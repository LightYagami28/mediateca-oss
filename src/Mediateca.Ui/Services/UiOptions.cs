namespace Mediateca.Ui.Services;

public sealed class UiOptions
{
    public const string SectionName = "Ui";

    public string Theme { get; set; } = "Dark";

    public double WindowWidth { get; set; } = 1440;

    public double WindowHeight { get; set; } = 900;

    public int SampleCatalogRows { get; set; } = 5000;

    public string ServerBindAddress { get; set; } = "127.0.0.1";

    public int ServerPort { get; set; } = 8083;

    /// <summary>Serve il server locale su HTTPS con certificato autofirmato.</summary>
    public bool ServerUseHttps { get; set; } = true;

    public string ServerApiToken { get; set; } = string.Empty;

    public string WebDavPasswordHash { get; set; } = string.Empty;

    public bool WebDavEnabled { get; set; } = true;

    public int SearchDebounceMs { get; set; } = 150;

    public string BotToken { get; set; } = string.Empty;

    public long[] BotAdminIds { get; set; } = [];

    public int MtprotoApiId { get; set; }

    public string MtprotoApiHash { get; set; } = string.Empty;

    public string MtprotoSessionPath { get; set; } = string.Empty;

    public string MtprotoCacheDirectory { get; set; } = string.Empty;
}
