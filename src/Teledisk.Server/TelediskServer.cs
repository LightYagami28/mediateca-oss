using System.Net;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Teledisk.Core.Abstractions;
using Teledisk.Core.Domain;
using Teledisk.Data;
using Teledisk.Licensing;

namespace Teledisk.Server;

/// <summary>Opzioni del server locale.</summary>
public sealed record ServerOptions
{
    /// <summary>Indirizzo di ascolto. <c>127.0.0.1</c> limita al dispositivo, <c>0.0.0.0</c> accetta la rete.</summary>
    public string BindAddress { get; init; } = "127.0.0.1";

    /// <summary>Porta di ascolto.</summary>
    public int Port { get; init; } = 8083;

    /// <summary>Abilita l'area WebDAV in sola lettura.</summary>
    public bool WebDavEnabled { get; init; } = true;

    /// <summary>Richiede autenticazione Basic sull'area WebDAV.</summary>
    public bool WebDavRequireAuth { get; init; } = true;

    /// <summary>Utente WebDAV.</summary>
    public string WebDavUser { get; init; } = "teledisk";

    /// <summary>Password WebDAV in chiaro, verificata all'avvio (alternativa all'hash).</summary>
    public string WebDavPassword { get; init; } = "";

    /// <summary>Hash PBKDF2 della password WebDAV: se presente ha la precedenza sul testo in chiaro.</summary>
    public string WebDavPasswordHash { get; init; } = "";

    /// <summary>Token per le API JSON. Vuoto significa nessuna autenticazione.</summary>
    public string ApiToken { get; init; } = "";
}

/// <summary>
/// Server HTTP locale con catalogo JSON e area WebDAV in sola lettura.
/// </summary>
/// <remarks>
/// Riproduce la superficie osservata nell'originale — server HTTP con bind configurabile e area
/// WebDAV read-only per rclone/Jellyfin/Emby — con due differenze volute: le API sono JSON
/// tipizzate e il gate <see cref="LicenseFeatures.RemoteAccess"/> decide se il bind può uscire
/// dal loopback.
/// </remarks>
public static class TelediskServer
{
    /// <summary>Percorso dell'area WebDAV.</summary>
    public const string DavPrefix = "/dav";

    /// <summary>Costruisce l'applicazione web.</summary>
    public static WebApplication Build(
        ServerOptions options,
        TelediskDatabase database,
        LicenseAuthority license,
        string? contentRoot = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(license);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = contentRoot ?? AppContext.BaseDirectory,
        });

        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        var app = builder.Build();

        var store = new SqliteMediaStore(database);
        // Nessuna chiave obbligatoria: ogni installazione ha tutte le funzionalità, quindi non
        // serve alcun controllo di licenza prima di esporre il server sulla rete.
        var status = license.Resolve();

        app.MapGet("/health", () => Results.Json(new
        {
            status = "ok",
            tier = status.Tier.ToString(),
            features = status.Features.ToString(),
            schema = database.CurrentVersion(),
        }));

        app.MapGet("/api/stats", (HttpContext context) =>
        {
            if (!Authorize(context, options))
            {
                return Results.Unauthorized();
            }

            var (items, bytes, channels) = store.Statistics();
            return Results.Json(new { items, bytes, channels, tier = status.Tier.ToString() });
        });

        app.MapGet("/api/items", (HttpContext context, string? q, string? kind, int? limit) =>
        {
            if (!Authorize(context, options))
            {
                return Results.Unauthorized();
            }

            MediaKind? filter = Enum.TryParse<MediaKind>(kind, ignoreCase: true, out var parsed) ? parsed : null;
            var items = store.Search(q ?? "", filter, Math.Clamp(limit ?? 100, 1, 1000));
            return Results.Json(items.Select(item => new
            {
                item.Id,
                item.Name,
                kind = item.Kind.ToString(),
                item.Size,
                item.Title,
                item.Year,
                item.Season,
                item.Episode,
                streamable = item.IsStreamable,
            }));
        });

        if (options.WebDavEnabled)
        {
            app.MapMethods(DavPrefix + "/{**path}", ["OPTIONS", "PROPFIND", "HEAD", "GET"], (HttpContext context, string? path) =>
                WebDav.Handle(context, path ?? "", store, database, options));
        }

        return app;
    }

    /// <summary>Avvia il server e attende la chiusura.</summary>
    public static async Task RunAsync(
        ServerOptions options,
        TelediskDatabase database,
        LicenseAuthority license,
        CancellationToken cancellationToken = default)
    {
        var app = Build(options, database, license);
        await app.StartAsync(cancellationToken).ConfigureAwait(false);
        app.Logger.LogInformation("teledisk in ascolto su http://{Address}:{Port}", options.BindAddress, options.Port);
        await app.WaitForShutdownAsync(cancellationToken).ConfigureAwait(false);
    }

    private static bool Authorize(HttpContext context, ServerOptions options)
    {
        if (string.IsNullOrEmpty(options.ApiToken))
        {
            return true;
        }

        var header = context.Request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
               && string.Equals(header[7..].Trim(), options.ApiToken, StringComparison.Ordinal);
    }
}

/// <summary>Area WebDAV in sola lettura sul catalogo, compatibile con rclone e media server.</summary>
internal static class WebDav
{
    private const string Namespace = "DAV:";

    internal static async Task Handle(
        HttpContext context,
        string path,
        SqliteMediaStore store,
        TelediskDatabase database,
        ServerOptions options)
    {
        if (options.WebDavRequireAuth && !CheckBasic(context, options))
        {
            context.Response.Headers.WWWAuthenticate = "Basic realm=\"Teledisk\"";
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            return;
        }

        switch (context.Request.Method)
        {
            case "OPTIONS":
                context.Response.Headers.Allow = "OPTIONS, PROPFIND, HEAD, GET";
                context.Response.Headers["DAV"] = "1";
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                return;

            case "PROPFIND":
                context.Response.StatusCode = 207;
                context.Response.ContentType = "application/xml; charset=utf-8";
                await context.Response.WriteAsync(BuildPropFind(path, store, database)).ConfigureAwait(false);
                return;

            case "HEAD":
            case "GET":
                var (status, _, _) = Describe(path, store, database);
                context.Response.StatusCode = status;
                context.Response.ContentType = "application/octet-stream";
                if (context.Request.Method == "HEAD")
                {
                    context.Response.ContentLength = 0;
                }

                return;
        }
    }

    private static (int Status, long Size, string Name) Describe(string path, SqliteMediaStore store, TelediskDatabase database)
    {
        var trimmed = path.Trim('/');
        if (string.IsNullOrEmpty(trimmed))
        {
            return ((int)HttpStatusCode.OK, database.CurrentVersion(), "/");
        }

        var matches = store.Search(trimmed, null, 1);
        return matches.Count > 0
            ? ((int)HttpStatusCode.OK, matches[0].Size, matches[0].Name)
            : ((int)HttpStatusCode.NotFound, 0, trimmed);
    }

    private static string BuildPropFind(string path, SqliteMediaStore store, TelediskDatabase database)
    {
        XNamespace dav = Namespace;
        var trimmed = path.Trim('/');

        var document = new XDocument(
            new XElement(dav + "multistatus",
                BuildEntry(dav, "/" + trimmed, isCollection: string.IsNullOrEmpty(trimmed), size: 0, modified: DateTimeOffset.UtcNow)));

        if (!string.IsNullOrEmpty(trimmed))
        {
            foreach (var item in store.Search(trimmed, null, 200))
            {
                document.Root!.Add(BuildEntry(dav, $"/{item.Name}", false, item.Size, item.UpdatedAt));
            }
        }

        return document.Declaration + Environment.NewLine + document;
    }

    private static XElement BuildEntry(XNamespace dav, string href, bool isCollection, long size, DateTimeOffset modified)
    {
        var response = new XElement(dav + "response",
            new XElement(dav + "href", href),
            new XElement(dav + "propstat",
                new XElement(dav + "prop",
                    new XElement(dav + "displayname", Path.GetFileName(href.Trim('/'))),
                    new XElement(dav + "getcontentlength", size),
                    new XElement(dav + "getlastmodified", modified.UtcDateTime.ToString("R")),
                    new XElement(dav + "resourcetype", isCollection ? new XElement(dav + "collection") : null))));

        return response;
    }

    private static bool CheckBasic(HttpContext context, ServerOptions options)
    {
        var hasHash = !string.IsNullOrEmpty(options.WebDavPasswordHash);
        if (!hasHash && string.IsNullOrEmpty(options.WebDavPassword))
        {
            return true;
        }

        var header = context.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(header[6..].Trim()));
            var separator = raw.IndexOf(':');
            if (separator <= 0)
            {
                return false;
            }

            var user = raw[..separator];
            var password = raw[(separator + 1)..];
            if (!string.Equals(user, options.WebDavUser, StringComparison.Ordinal))
            {
                return false;
            }

            // Le impostazioni salvano l'hash, non la password: se c'è, si verifica quello.
            return hasHash
                ? Teledisk.Core.Security.PasswordHasher.Verify(password, options.WebDavPasswordHash)
                : string.Equals(password, options.WebDavPassword, StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
