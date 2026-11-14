namespace Teledisk.Ui.Services;

public sealed class UiOptions
{
    public const string SectionName = "Ui";

    public string Theme { get; init; } = "Dark";

    public double WindowWidth { get; init; } = 1440;

    public double WindowHeight { get; init; } = 900;

    public int SampleCatalogRows { get; init; } = 5000;
}
