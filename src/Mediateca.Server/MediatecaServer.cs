using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mediateca.Core.Abstractions;
using Mediateca.Core.Domain;
using Mediateca.Data;

namespace Mediateca.Server;

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
    public string WebDavUser { get; init; } = "mediateca";

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
/// tipizzate e ogni installazione ha tutte le funzionalità, senza chiavi né attivazioni.
/// </remarks>
public static class MediatecaServer
{
    /// <summary>Percorso dell'area WebDAV.</summary>
    public const string DavPrefix = "/dav";

    /// <summary>Costruisce l'applicazione web.</summary>
    public static WebApplication Build(
        ServerOptions options,
        MediatecaDatabase database,
        string? contentRoot = null,
        IMediaFileSource? fileSource = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(database);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = contentRoot ?? AppContext.BaseDirectory,
        });

        builder.WebHost.ConfigureKestrel(kestrel => kestrel.AddServerHeader = false);

        // La porta e l'indirizzo delle opzioni devono arrivare a Kestrel: senza questo il server
        // ascolterebbe sulle impostazioni predefinite di ASP.NET e ignorerebbe la configurazione.
        // Se ASPNETCORE_URLS e' impostata (utile nei test) ha la precedenza.
        var configuredUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
        builder.WebHost.UseUrls(string.IsNullOrWhiteSpace(configuredUrls)
            ? $"http://{options.BindAddress}:{options.Port}"
            : configuredUrls);

        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        var app = builder.Build();

        app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            if (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/dav"))
            {
                context.Response.Headers["Cache-Control"] = "no-store";
            }

            await next().ConfigureAwait(false);
        });

        var store = new SqliteMediaStore(database);

        app.MapGet("/health", () => Results.Json(new
        {
            status = "ok",
            schema = database.CurrentVersion(),
        }));

        app.MapGet("/api/stats", (HttpContext context) =>
        {
            if (!Authorize(context, options))
            {
                return Results.Unauthorized();
            }

            var (items, bytes, channels) = store.Statistics();
            return Results.Json(new { items, bytes, channels });
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
                WebDav.Handle(context, path ?? "", store, database, options, fileSource));
        }

        app.MapMethods("/api/items/{id:long}/stream", ["GET", "HEAD"], async (HttpContext context, long id) =>
            await MediaApi.Stream(context, id, store, fileSource, options).ConfigureAwait(false));

        app.MapGet("/miniapp", () => Results.Content(Miniapp.Html, "text/html; charset=utf-8"));

        return app;
    }

    /// <summary>Avvia il server e attende la chiusura.</summary>
    public static async Task RunAsync(
        ServerOptions options,
        MediatecaDatabase database,
        CancellationToken cancellationToken = default)
    {
        var app = Build(options, database);
        await app.StartAsync(cancellationToken).ConfigureAwait(false);
        app.Logger.LogInformation("mediateca in ascolto su http://{Address}:{Port}", options.BindAddress, options.Port);
        await app.WaitForShutdownAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static bool Authorize(HttpContext context, ServerOptions options)
    {
        if (string.IsNullOrEmpty(options.ApiToken))
        {
            return true;
        }

        var header = context.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var supplied = header[7..].Trim();
        var expected = options.ApiToken;
        if (supplied.Length != expected.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(supplied),
            Encoding.UTF8.GetBytes(expected));
    }
}

/// <summary>Area WebDAV in sola lettura sul catalogo, compatibile con rclone e media server.</summary>
internal static class WebDav
{
    private const string Namespace = "DAV:";

    private const int MaxBasicFailures = 10;

    private const long BasicWindowMs = 60_000;

    private static readonly Dictionary<string, (int Count, long Window)> BasicFailures = [];

    private static readonly Lock BasicGate = new();

    internal static async Task Handle(
        HttpContext context,
        string path,
        SqliteMediaStore store,
        MediatecaDatabase database,
        ServerOptions options,
        IMediaFileSource? source)
    {
        if (IsThrottled(context))
        {
            context.Response.StatusCode = 429;
            return;
        }

        if (options.WebDavRequireAuth && !CheckBasic(context, options))
        {
            RecordFailure(context);
            context.Response.Headers.WWWAuthenticate = "Basic realm=\"Mediateca\"";
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            return;
        }

        ResetFailures(context);

        switch (context.Request.Method)
        {
            case "OPTIONS":
                context.Response.Headers.Allow = "OPTIONS, PROPFIND, HEAD, GET";
                context.Response.Headers["DAV"] = "1";
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                return;

            case "PROPFIND":
                context.Response.StatusCode = 207;
                context.Response.Headers["DAV"] = "1";
                context.Response.ContentType = "application/xml; charset=utf-8";
                await context.Response.WriteAsync(BuildPropFind(path, store, database)).ConfigureAwait(false);
                return;

            case "HEAD":
            case "GET":
                if (source is not null)
                {
                    var matches = store.Search(path.Trim('/'), null, 1);
                    if (matches.Count > 0)
                    {
                        await MediaApi.StreamItem(context, matches[0], source).ConfigureAwait(false);
                        return;
                    }

                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    return;
                }

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

    private static (int Status, long Size, string Name) Describe(string path, SqliteMediaStore store, MediatecaDatabase database)
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

    private static string BuildPropFind(string path, SqliteMediaStore store, MediatecaDatabase database)
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
    {        var hasHash = !string.IsNullOrEmpty(options.WebDavPasswordHash);
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
                ? Mediateca.Core.Security.PasswordHasher.Verify(password, options.WebDavPasswordHash)
                : string.Equals(password, options.WebDavPassword, StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsThrottled(HttpContext context)
    {
        var key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        lock (BasicGate)
        {
            return BasicFailures.TryGetValue(key, out var entry)
                   && entry.Count >= MaxBasicFailures
                   && entry.Window + BasicWindowMs >= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    private static void RecordFailure(HttpContext context)
    {
        var key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        lock (BasicGate)
        {
            BasicFailures.TryGetValue(key, out var entry);
            var window = entry.Window + BasicWindowMs >= now ? entry.Window : now;
            BasicFailures[key] = (entry.Count + 1, window);
        }
    }

    private static void ResetFailures(HttpContext context)
    {
        var key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        lock (BasicGate)
        {
            BasicFailures.Remove(key);
        }
    }
}
