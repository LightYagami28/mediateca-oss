using System.Globalization;
using System.Text.RegularExpressions;
using Mediateca.Core.Abstractions;

namespace Mediateca.Core.Domain;

public sealed record ParsedMediaName(MediaKind Kind, string? Title, int? Year, int? Season, int? Episode);

public static partial class MediaNameParser
{
    public static ParsedMediaName Parse(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var name = Path.GetFileName(fileName.Replace('\\', '/'));
        var kind = KindFromExtension(name);
        if (kind is not (MediaKind.Movie or MediaKind.Music or MediaKind.Book))
        {
            return new ParsedMediaName(kind, null, null, null, null);
        }

        var text = LeadingGroup().Replace(Path.GetFileNameWithoutExtension(name), "");
        text = Separators().Replace(text, " ");

        int? season = null;
        int? episode = null;
        var cut = text.Length;

        var series = SeasonEpisode().Match(text);
        if (kind == MediaKind.Movie && series.Success)
        {
            season = Number(series.Groups["s1"].Success ? series.Groups["s1"] : series.Groups["s2"]);
            episode = Number(series.Groups["e1"].Success ? series.Groups["e1"] : series.Groups["e2"]);
            kind = MediaKind.Episode;
            cut = series.Index;
        }

        var tag = QualityTag().Match(text);
        if (tag.Success && tag.Index > 0)
        {
            cut = Math.Min(cut, tag.Index);
        }

        var latestYear = DateTime.UtcNow.Year + 1;
        var yearMatch = YearPattern().Matches(text)
            .LastOrDefault(match => match.Index > 0 && match.Index < cut && Number(match.Groups["year"]) <= latestYear);
        int? year = yearMatch is null ? null : Number(yearMatch.Groups["year"]);
        if (yearMatch is not null)
        {
            cut = yearMatch.Index;
        }

        var title = Spaces().Replace(text[..cut], " ").Trim(' ', '-', '(', '[', '.');
        return new ParsedMediaName(kind, title.Length == 0 ? null : title, year, season, episode);
    }

    public static MediaKind KindFromExtension(string name)
    {
        if (MediaExtensions.IsVideo(name)) return MediaKind.Movie;
        if (MediaExtensions.IsAudio(name)) return MediaKind.Music;
        if (MediaExtensions.IsImage(name)) return MediaKind.Image;

        return Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".zip" or ".rar" or ".7z" => MediaKind.Archive,
            ".epub" or ".mobi" or ".azw3" => MediaKind.Book,
            ".pdf" or ".docx" => MediaKind.Document,
            _ => MediaKind.Unknown,
        };
    }

    private static int Number(Group group) => int.Parse(group.Value, NumberStyles.None, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^\s*\[[^\]]*\]\s*")]
    private static partial Regex LeadingGroup();

    [GeneratedRegex(@"[._]+")]
    private static partial Regex Separators();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"\b(?:[Ss](?<s1>\d{1,2})\s?[Ee](?<e1>\d{1,3})|(?<s2>\d{1,2})x(?<e2>\d{2,3}))\b")]
    private static partial Regex SeasonEpisode();

    [GeneratedRegex(@"[\(\[\s](?<year>(?:19|20)\d{2})(?=[\)\]\s]|$)")]
    private static partial Regex YearPattern();

    [GeneratedRegex(@"\b(?:2160p|1080p|720p|480p|4k|uhd|x264|x265|h264|h265|hevc|bluray|bdrip|brrip|web-?dl|webrip|hdtv|dvdrip|hdrip|remux|ita|eng|multi)\b", RegexOptions.IgnoreCase)]
    private static partial Regex QualityTag();
}
