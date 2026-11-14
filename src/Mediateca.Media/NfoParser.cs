using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Mediateca.Media;

public sealed record NfoMetadata
{
    public string? Title { get; init; }

    public string? OriginalTitle { get; init; }

    public string? Plot { get; init; }

    public string? Tagline { get; init; }

    public int? Year { get; init; }

    public IReadOnlyList<string> Genres { get; init; } = [];

    public double? Rating { get; init; }

    public int? RuntimeMinutes { get; init; }
}

public static class NfoParser
{
    public static NfoMetadata? Parse(string xml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);

        XDocument document;
        try
        {
            document = XDocument.Parse(xml, LoadOptions.None);
        }
        catch (XmlException)
        {
            return null;
        }

        var root = document.Descendants().FirstOrDefault(element =>
            element.Name.LocalName is "movie" or "tvshow" or "episode");
        if (root is null)
        {
            return null;
        }

        string? Value(string name) => root.Descendants().FirstOrDefault(element => element.Name.LocalName == name)?.Value.Trim();

        var genres = root.Descendants()
            .Where(element => element.Name.LocalName == "genre")
            .Select(element => element.Value.Trim())
            .Where(genre => genre.Length > 0)
            .ToList();

        int? year = int.TryParse(Value("year"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedYear)
            ? parsedYear
            : null;
        double? rating = double.TryParse(Value("rating"), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedRating)
            ? parsedRating
            : null;
        int? runtime = int.TryParse(Value("runtime"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedRuntime)
            ? parsedRuntime
            : null;

        return new NfoMetadata
        {
            Title = Value("title"),
            OriginalTitle = Value("originaltitle"),
            Plot = Value("plot"),
            Tagline = Value("tagline"),
            Year = year,
            Genres = genres,
            Rating = rating,
            RuntimeMinutes = runtime,
        };
    }
}
