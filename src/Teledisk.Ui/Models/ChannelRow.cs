using System.Globalization;
using Teledisk.Core.Domain;

namespace Teledisk.Ui.Models;

public sealed record ChannelRow
{
    public required long Id { get; init; }

    public required string Title { get; init; }

    public required string? UsernameRaw { get; init; }

    public required string Kind { get; init; }

    public int ItemCount { get; init; }

    public bool IsSource { get; init; }

    public string? LastScanRaw { get; init; }

    public string Username => string.IsNullOrEmpty(UsernameRaw) ? "—" : "@" + UsernameRaw;

    public string SourceText => IsSource ? "Sorgente" : "—";

    public string LastScanText => DateTimeOffset.TryParse(LastScanRaw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var stamp)
        ? Format.Date(stamp)
        : "mai";

    public static ChannelRow From(Channel channel) => new()
    {
        Id = channel.Id,
        Title = channel.Title,
        UsernameRaw = channel.Username,
        Kind = channel.Kind.ToString(),
        ItemCount = channel.ItemCount,
        IsSource = channel.IsSource,
        LastScanRaw = channel.LastScanAt?.UtcDateTime.ToString("O"),
    };
}
