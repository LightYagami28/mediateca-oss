namespace Teledisk.Ui.Services;

public sealed class UiOptions
{
    public const string SectionName = "Ui";

    public string Theme { get; set; } = "Dark";

    public double WindowWidth { get; set; } = 1440;

    public double WindowHeight { get; set; } = 900;

    public int SampleCatalogRows { get; set; } = 5000;

    public string ServerBindAddress { get; set; } = "127.0.0.1";

    public int ServerPort { get; set; } = 8083;

    public string ServerApiToken { get; set; } = string.Empty;

    public string WebDavPasswordHash { get; set; } = string.Empty;

    public bool WebDavEnabled { get; set; } = true;

    public int SearchDebounceMs { get; set; } = 150;
}
