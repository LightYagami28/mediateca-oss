using System.Text.Json;
using Mediateca.Core.Domain;

namespace Mediateca.Media;

public sealed class MusicBrainzScraper(HttpClient httpClient) : IMediaScraper
{
    public string Name => "musicbrainz";

    public bool IsConfigured => true;

    public async Task<IReadOnlyList<MediaCandidate>> SearchAsync(MediaKind kind, string query, int? year = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var url = BuildSearchUrl(query, year);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", "Mediateca/1.0 (community client)");

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("recordings", out var recordings) || recordings.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return ParseRecordings(recordings, year);
    }

    private static string BuildSearchUrl(string query, int? year)
    {
        var url = $"https://musicbrainz.org/ws/2/recording/?query={Uri.EscapeDataString(query)}&fmt=json&limit=10";
        return year is int releaseYear ? $"{url}&firstreleasedate={releaseYear}" : url;
    }

    private static IReadOnlyList<MediaCandidate> ParseRecordings(JsonElement recordings, int? year)
    {
        var results = new List<MediaCandidate>();
        foreach (var recording in recordings.EnumerateArray())
        {
            var candidate = ParseRecording(recording, year);
            if (candidate is not null)
            {
                results.Add(candidate);
            }
        }

        return results;
    }

    private static MediaCandidate? ParseRecording(JsonElement recording, int? year)
    {
        var id = GetString(recording, "id");
        var title = GetString(recording, "title");
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(title))
        {
            return null;
        }

        var artist = ExtractArtist(recording);
        return new MediaCandidate
        {
            Id = id,
            Kind = MediaKind.Music,
            Title = artist is null ? title : $"{artist} — {title}",
            Year = ExtractYear(recording, year),
        };
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) ? value.GetString() : null;

    private static string? ExtractArtist(JsonElement recording) =>
        recording.TryGetProperty("artist-credit", out var credit) && credit.ValueKind == JsonValueKind.Array
            ? credit.EnumerateArray()
                .Select(entry => GetString(entry, "name"))
                .FirstOrDefault(name => !string.IsNullOrEmpty(name))
            : null;

    private static int? ExtractYear(JsonElement recording, int? fallback) =>
        recording.TryGetProperty("first-release-date", out var dateElement)
        && int.TryParse(dateElement.GetString()?.Split('-')[0], out var parsed)
            ? parsed
            : fallback;

    public Task<MediaDetails?> DetailsAsync(MediaKind kind, string id, CancellationToken cancellationToken = default) =>
        Task.FromResult<MediaDetails?>(null);
}
