namespace Teledisk.Core.Abstractions;

/// <summary>Impostazioni tipizzate dell'installazione.</summary>
public sealed record TelediskSettings
{
    /// <summary>Lingua dell'interfaccia, codice ISO a due lettere.</summary>
    public string Language { get; init; } = "it";

    /// <summary>Radice dei dati dell'utente.</summary>
    public string RootDirectory { get; init; } = "";

    /// <summary>Indirizzo di ascolto del server HTTP. <c>127.0.0.1</c> limita al dispositivo.</summary>
    public string ServerBindAddress { get; init; } = "127.0.0.1";

    /// <summary>Porta reale di ascolto.</summary>
    public int ServerBindPort { get; init; } = 8083;

    /// <summary>Porta pubblica dichiarata negli URL generati, quando diversa da quella di ascolto.</summary>
    public int? ServerPublicPort { get; init; }

    /// <summary>Base URL pubblico (per esempio un DDNS) usato nei link.</summary>
    public string PublicBaseUrl { get; init; } = "";

    /// <summary>Abilita il server WebDAV in sola lettura.</summary>
    public bool WebDavEnabled { get; init; }

    /// <summary>Consente l'accesso WebDAV dalla rete locale.</summary>
    public bool WebDavAllowLan { get; init; }

    /// <summary>Richiede autenticazione sul WebDAV.</summary>
    public bool WebDavRequireAuth { get; init; } = true;

    /// <summary>Utente WebDAV.</summary>
    public string WebDavUser { get; init; } = "teledisk";

    /// <summary>Hash della password WebDAV.</summary>
    public string WebDavPasswordHash { get; init; } = "";

    /// <summary>Esegue un backup locale ogni giorno all'ora indicata.</summary>
    public bool BackupEnabled { get; init; }

    /// <summary>Ora locale del backup giornaliero, formato <c>HH:mm</c>.</summary>
    public string BackupTime { get; init; } = "03:00";

    /// <summary>Numero di backup da conservare.</summary>
    public int BackupRetention { get; init; } = 14;

    /// <summary>Modalità tema: <c>system</c>, <c>light</c> o <c>dark</c>.</summary>
    public string Theme { get; init; } = "system";

    /// <summary>Scala dell'interfaccia.</summary>
    public double UiScale { get; init; } = 1.0;

    /// <summary>Estensioni considerate video.</summary>
    public IReadOnlyList<string> VideoExtensions { get; init; } = [".mp4", ".mkv", ".avi", ".mov", ".webm", ".m4v", ".wmv", ".flv"];

    /// <summary>Estensioni considerate immagini.</summary>
    public IReadOnlyList<string> ImageExtensions { get; init; } = [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff"];

    /// <summary>Estensioni considerate documenti.</summary>
    public IReadOnlyList<string> DocumentExtensions { get; init; } = [".pdf", ".epub", ".mobi", ".txt", ".doc", ".docx", ".odt", ".rtf", ".csv"];
}

/// <summary>Accesso persistente alle impostazioni.</summary>
public interface ISettingsStore
{
    /// <summary>Legge le impostazioni correnti.</summary>
    TelediskSettings Load();

    /// <summary>Salva le impostazioni.</summary>
    void Save(TelediskSettings settings);
}

/// <summary>Elenco di estensioni riconosciute per tipo di contenuto.</summary>
public static class MediaExtensions
{
    /// <summary>True quando l'estensione è considerata video.</summary>
    public static bool IsVideo(string name) =>
        name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".avi", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".mov", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".webm", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".m4v", StringComparison.OrdinalIgnoreCase);

    /// <summary>True quando l'estensione è considerata audio.</summary>
    public static bool IsAudio(string name) =>
        name.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".flac", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".wav", StringComparison.OrdinalIgnoreCase);

    /// <summary>True quando l'estensione è considerata immagine.</summary>
    public static bool IsImage(string name) =>
        name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);
}
