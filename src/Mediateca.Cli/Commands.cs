using System.Text;
using Mediateca.Backup;
using Mediateca.Core.Abstractions;
using Mediateca.Core.Domain;
using Mediateca.Core.Infrastructure;
using Mediateca.Core.Security;
using Mediateca.Core.Settings;
using Mediateca.Data;
using Mediateca.Server;

namespace Mediateca.Cli;

/// <summary>
/// Comandi operativi della CLI: database, impostazioni, scansione locale, backup e server.
/// </summary>
/// <remarks>
/// Tutti i comandi lavorano sulla stessa radice dati e sullo stesso database dell'applicazione,
/// così la CLI è utilizzabile come strumento di manutenzione e come motore headless.
/// </remarks>
internal static class Commands
{
    /// <summary>Percorsi dell'installazione.</summary>
    internal static MediatecaPaths Paths() => MediatecaPaths.Default();

    /// <summary>Inizializza il database e riporta la versione schema.</summary>
    internal static int DatabaseInit()
    {
        var paths = Paths();
        paths.EnsureCreated();

        var database = new MediatecaDatabase(paths.DatabasePath);
        var version = database.Migrate();
        var healthy = database.CheckIntegrity(out var detail);

        Console.WriteLine($"database : {paths.DatabasePath}");
        Console.WriteLine($"schema   : {version}");
        Console.WriteLine($"integrità: {(healthy ? "ok" : detail)}");
        return healthy ? 0 : 2;
    }

    /// <summary>Mostra o modifica le impostazioni.</summary>
    internal static int Settings(string[] args)
    {
        var paths = Paths();
        paths.EnsureCreated();

        var store = new JsonSettingsStore(paths.SettingsPath);
        var settings = store.Load();

        if (args.Length == 0 || args[0] == "show")
        {
            ShowSettings(settings, store.Path);
            return 0;
        }

        if (args[0] == "set")
        {
            ApplySettings(settings, store, args);
            return 0;
        }

        if (args[0] == "password")
        {
            SetPassword(settings, store, args);
            return 0;
        }

        throw new ArgumentException($"sottocomando sconosciuto '{args[0]}'");
    }

    private static void ShowSettings(MediatecaSettings settings, string path)
    {
        Console.WriteLine($"file          : {path}");
        Console.WriteLine($"lingua        : {settings.Language}");
        Console.WriteLine($"bind          : {settings.ServerBindAddress}:{settings.ServerBindPort}");
        Console.WriteLine($"url pubblico  : {(settings.PublicBaseUrl?.ToString() ?? "(nessuno)")}");
        Console.WriteLine($"webdav        : {(settings.WebDavEnabled ? "attivo" : "spento")} (auth: {settings.WebDavRequireAuth})");
        Console.WriteLine($"backup        : {(settings.BackupEnabled ? $"giornaliero alle {settings.BackupTime}" : "spento")}");
    }

    private static void ApplySettings(MediatecaSettings settings, JsonSettingsStore store, string[] args)
    {
        for (var index = 1; index + 1 < args.Length; index += 2)
        {
            var key = args[index];
            var value = args[index + 1];
            settings = key switch
            {
                "language" => settings with { Language = value },
                "bind" => settings with { ServerBindAddress = value },
                "port" => settings with { ServerBindPort = int.Parse(value) },
                "public-url" => settings with { PublicBaseUrl = ParseHttpsUrl(value) },
                "webdav" => settings with { WebDavEnabled = bool.Parse(value) },
                "backup" => settings with { BackupEnabled = bool.Parse(value) },
                "backup-time" => settings with { BackupTime = value },
                "theme" => settings with { Theme = value },
                _ => throw new ArgumentException($"chiave sconosciuta '{key}'"),
            };
        }

        store.Save(settings);
        Console.WriteLine($"salvate in {store.Path}");
    }

    private static Uri ParseHttpsUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps
            ? url
            : throw new ArgumentException("public-url deve essere un URL assoluto https.");

    private static void SetPassword(MediatecaSettings settings, JsonSettingsStore store, string[] args)
    {
        var password = args.Length > 1 ? args[1] : throw new ArgumentException("uso: settings password <valore>");
        var hash = PasswordHasher.Hash(password);
        store.Save(settings with { WebDavPasswordHash = hash, WebDavRequireAuth = true });
        Console.WriteLine($"hash WebDAV impostato ({PasswordHasher.IsHash(hash)})");
    }

    /// <summary>Importa nel catalogo i file di una cartella locale.</summary>
    internal static int Scan(string[] args)
    {
        var directory = args.Length > 0 ? args[0] : throw new ArgumentException("uso: scan <cartella> [--channel <id>]");
        if (!Directory.Exists(directory))
        {
            Console.Error.WriteLine($"cartella inesistente: {directory}");
            return 1;
        }

        var channelId = Array.IndexOf(args, "--channel") is var channelIndex && channelIndex >= 0 && channelIndex + 1 < args.Length
            ? long.Parse(args[channelIndex + 1])
            : 0L;

        var paths = Paths();
        paths.EnsureCreated();
        var database = new MediatecaDatabase(paths.DatabasePath);
        database.Migrate();
        var store = new SqliteMediaStore(database);
        store.UpsertChannel(new Channel
        {
            Id = channelId,
            Title = channelId == 0 ? "Locale" : $"Canale {channelId}",
            Kind = ChannelKind.Saved,
            IsSource = true,
            LastScanAt = DateTimeOffset.UtcNow,
        });

        var messageId = 0;
        var imported = 0;
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            var info = new FileInfo(file);
            var parsed = MediaNameParser.Parse(info.Name);
            store.Upsert(new MediaItem
            {
                ChannelId = channelId,
                MessageId = ++messageId,
                Name = info.Name,
                Kind = parsed.Kind,
                Title = parsed.Title,
                Year = parsed.Year,
                Season = parsed.Season,
                Episode = parsed.Episode,
                Size = info.Length,
                UpdatedAt = info.LastWriteTimeUtc,
            });
            imported++;
        }

        Console.WriteLine($"importati: {imported} file da {directory}");
        var (items, bytes, _) = store.Statistics();
        Console.WriteLine($"catalogo : {items} elementi, {bytes / (1024.0 * 1024):N1} MiB");
        return 0;
    }

    /// <summary>Crea un backup compresso della radice dati.</summary>
    internal static async Task<int> Backup(string[] args)
    {
        var paths = Paths();
        paths.EnsureCreated();

        var retention = int.TryParse(Option(args, "--keep"), out var keep) ? keep : 14;
        var local = new LocalBackupTarget(retention);
        BackupResult result;
        if (args.Contains("--drive"))
        {
            var options = GoogleDriveOptions.FromEnvironment() with { MaxBackups = retention };
            using var http = new HttpClient { Timeout = TimeSpan.FromHours(1) };
            result = await new GoogleDriveBackupTarget(http, options, local).CreateAsync(paths.Root, paths.BackupDirectory).ConfigureAwait(false);
        }
        else
        {
            result = await local.CreateAsync(paths.Root, paths.BackupDirectory).ConfigureAwait(false);
        }

        Console.WriteLine($"backup   : {result.ArchivePath}");
        Console.WriteLine($"dimensione: {result.SizeBytes / 1024.0:N0} KiB");
        Console.WriteLine($"sha256   : {result.Sha256}");
        if (result.RemoteId is not null)
        {
            Console.WriteLine($"drive    : {result.RemoteId}");
        }

        Console.WriteLine($"rimossi  : {result.RemovedOldArchives}");
        return 0;
    }

    /// <summary>Avvia il server HTTP/WebDAV.</summary>
    internal static async Task<int> Serve(string[] args)
    {
        var paths = Paths();
        paths.EnsureCreated();

        var settings = new JsonSettingsStore(paths.SettingsPath).Load();
        var bind = Option(args, "--bind") ?? settings.ServerBindAddress;
        var port = int.TryParse(Option(args, "--port"), out var parsed) ? parsed : settings.ServerBindPort;
        var token = Option(args, "--token") ?? Environment.GetEnvironmentVariable("MEDIATECA_API_TOKEN") ?? "";

        var database = new MediatecaDatabase(paths.DatabasePath);
        database.Migrate();

        var options = MediatecaServer.ApplyNonLoopbackGuards(new ServerOptions
        {
            BindAddress = bind,
            Port = port,
            WebDavEnabled = settings.WebDavEnabled,
            WebDavRequireAuth = settings.WebDavRequireAuth,
            WebDavUser = settings.WebDavUser,
            WebDavPasswordHash = settings.WebDavPasswordHash,
            ApiToken = token,
        });

        await MediatecaServer.WriteGuardWarningsAsync(Console.Error, bind, options, settings.WebDavEnabled).ConfigureAwait(false);

        Console.WriteLine($"mediateca server su {Uri.UriSchemeHttps}://{options.BindAddress}:{options.Port}");
        Console.WriteLine($"webdav {(options.WebDavEnabled ? "attivo" : "spento")}, api token {(string.IsNullOrEmpty(options.ApiToken) ? "assente" : "presente")}");
        Console.WriteLine("Ctrl+C per fermare");

        await MediatecaServer.RunAsync(options, database);

        return 0;
    }

    private static string? Option(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
