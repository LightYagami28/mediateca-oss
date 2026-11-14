namespace Mediateca.Core.Infrastructure;

/// <summary>
/// Percorsi dell'installazione, tutti derivati da un'unica radice dati.
/// </summary>
/// <remarks>
/// La radice è scelta in questo ordine: <c>MEDIATECA_HOME</c>, poi <c>%APPDATA%\Mediateca</c> su
/// Windows o <c>~/.local/share/Mediateca</c> altrove. Tutto ciò che è scrivibile vive qui: il
/// bundle dell'applicazione resta di sola lettura, come nell'originale.
/// </remarks>
public sealed class MediatecaPaths
{
    /// <summary>Variabile che sostituisce la radice dati.</summary>
    public const string HomeVariable = "MEDIATECA_HOME";

    /// <summary>Crea i percorsi a partire da una radice esplicita.</summary>
    public MediatecaPaths(string root)
    {
        Root = Path.GetFullPath(root);
    }

    /// <summary>Radice dati risolta dall'ambiente.</summary>
    public static MediatecaPaths Default() => new(ResolveRoot());

    /// <summary>Radice dati.</summary>
    public string Root { get; }

    /// <summary>Database principale.</summary>
    public string DatabasePath => Path.Combine(Root, "database.db");

    /// <summary>Impostazioni dell'utente.</summary>
    public string SettingsPath => Path.Combine(Root, "settings.json");

    /// <summary>Directory dei backup.</summary>
    public string BackupDirectory => Path.Combine(Root, "backups");

    /// <summary>Cache delle copertine e delle anteprime.</summary>
    public string CacheDirectory => Path.Combine(Root, "cache");

    /// <summary>Log applicativi.</summary>
    public string LogDirectory => Path.Combine(Root, "logs");

    /// <summary>Crea tutte le directory necessarie.</summary>
    public void EnsureCreated()
    {
        foreach (var directory in new[] { Root, BackupDirectory, CacheDirectory, LogDirectory })
        {
            Directory.CreateDirectory(directory);
        }
    }

    /// <summary>Radice dati risolta dall'ambiente del processo.</summary>
    public static string ResolveRoot()
    {
        var custom = Environment.GetEnvironmentVariable(HomeVariable);
        if (!string.IsNullOrWhiteSpace(custom))
        {
            return Path.GetFullPath(custom);
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return string.IsNullOrWhiteSpace(appData)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "Mediateca")
            : Path.Combine(appData, "Mediateca");
    }
}
