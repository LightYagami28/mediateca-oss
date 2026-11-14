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

        var url = $"https://musicbrainz.org/ws/2/recording/?query={Uri.EscapeDataString(query)}&fmt=json&limit=10";
        if (year is int releaseYear)
        {
            url += $"&firstreleasedate={releaseYear}";
        }

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

        var results = new List<MediaCandidate>();
        foreach (var recording in recordings.EnumerateArray())
        {
            var id = recording.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
            var title = recording.TryGetProperty("title", out var titleElement) ? titleElement.GetString() : null;
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(title))
            {
                continue;
            }

            var artist = recording.TryGetProperty("artist-credit", out var credit) && credit.ValueKind == JsonValueKind.Array
                ? credit.EnumerateArray().Select(entry =>
                    entry.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null)
                    .FirstOrDefault(name => !string.IsNullOrEmpty(name))
                : null;

            var parsedYear = recording.TryGetProperty("first-release-date", out var dateElement)
                             && int.TryParse(dateElement.GetString()?.Split('-')[0], out var parsed)
                ? parsed
                : year;

            results.Add(new MediaCandidate
            {
                Id = id,
                Kind = MediaKind.Music,
                Title = artist is null ? title : $"{artist} — {title}",
                Year = parsedYear,
            });
        }

        return results;
    }

    public Task<MediaDetails?> DetailsAsync(MediaKind kind, string id, CancellationToken cancellationToken = default) =>
        Task.FromResult<MediaDetails?>(null);
}
