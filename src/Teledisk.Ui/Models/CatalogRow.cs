namespace Teledisk.Ui.Models;

public sealed record CatalogRow
{
    public required long Id { get; init; }

    public required string Name { get; init; }

    public required string Extension { get; init; }

    public required string KindLabel { get; init; }

    public required string KindColor { get; init; }

    public required string Channel { get; init; }

    public long Size { get; init; }

    public int? DurationSeconds { get; init; }

    public int? Year { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public string SizeText => Format.Size(Size);

    public string DurationText => DurationSeconds is null ? string.Empty : Format.Duration(DurationSeconds.Value);

    public string UpdatedText => Format.Date(UpdatedAt);
}

public static class Format
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB"];

    public static string Size(long bytes)
    {
        if (bytes <= 0)
        {
            return "—";
        }

        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{bytes} B"
            : $"{value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)} {Units[unit]}";
    }

    public static string Duration(int seconds)
    {
        var span = TimeSpan.FromSeconds(seconds);
        return span.TotalHours >= 1
            ? $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}"
            : $"{span.Minutes}:{span.Seconds:00}";
    }

    public static string Date(DateTimeOffset value)
    {
        var local = value.ToLocalTime();
        var today = DateTimeOffset.Now.Date;
        return local.Date == today
            ? local.ToString("HH:mm")
            : local.ToString("dd/MM/yyyy");
    }
}