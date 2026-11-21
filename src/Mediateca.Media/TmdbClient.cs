using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mediateca.Core.Domain;

namespace Mediateca.Media;

/// <summary>
/// Opzioni di accesso alle API pubbliche TMDB v3.
/// </summary>
/// <remarks>
/// La chiave viene letta dalla variabile <see cref="ApiKeyVariable"/> solo quando non è passata
/// esplicitamente: così la configurazione dell'applicazione può sempre avere la precedenza.
/// </remarks>
public sealed record TmdbOptions
{
    /// <summary>Variabile d'ambiente da cui leggere la chiave quando non è passata.</summary>
    public const string ApiKeyVariable = "MEDIATECA_TMDB_API_KEY";

    /// <summary>Host predefinito delle API v3.</summary>
    public const string ApiHost = "api.themoviedb.org";

    /// <summary>Indirizzo predefinito delle API v3, composto senza URI assoluti hardcoded.</summary>
    public static string DefaultBaseUrl { get; } = new UriBuilder(Uri.UriSchemeHttps, ApiHost, -1, "/3").Uri.ToString().TrimEnd('/');

    /// <summary>Lingua predefinita delle risposte.</summary>
    public const string DefaultLanguage = "it-IT";

    /// <summary>Chiave API, presa dalla variabile <see cref="ApiKeyVariable"/> se non specificata.</summary>
    public string ApiKey { get; init; } = Environment.GetEnvironmentVariable(ApiKeyVariable)?.Trim() ?? "";

    /// <summary>Radice delle API; sovrascrivibile per proxy o mirror.</summary>
    public string BaseUrl { get; init; } = DefaultBaseUrl;

    /// <summary>Lingua delle risposte, nel formato TMDB (<c>it-IT</c>).</summary>
    public string Language { get; init; } = DefaultLanguage;

    /// <summary>True quando è disponibile una chiave API.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}

/// <summary>
/// Client delle API pubbliche TMDB v3.
/// </summary>
/// <remarks>
/// Senza chiave il client resta utilizzabile: <see cref="IsConfigured"/> è <c>false</c> e le
/// ricerche restituiscono subito un risultato vuoto senza toccare la rete. Il dettaglio esposto è
/// quello che serve al catalogo: titolo, trama, immagini, voto, generi e conteggi.
/// </remarks>
public sealed class TmdbClient : IMediaScraper, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http;
    private readonly TmdbOptions _options;
    private readonly bool _ownsHttp;

    /// <summary>Crea il client sull'<see cref="HttpClient"/> iniettato.</summary>
    /// <param name="httpClient">Client HTTP condiviso dal chiamante.</param>
    /// <param name="options">Opzioni di accesso; con <c>null</c> valgono i valori predefiniti.</param>
    public TmdbClient(HttpClient httpClient, TmdbOptions? options = null)
        : this(httpClient, options ?? new TmdbOptions(), ownsHttp: false)
    {
    }

    private TmdbClient(HttpClient httpClient, TmdbOptions options, bool ownsHttp)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options;
        _ownsHttp = ownsHttp;
    }

    /// <summary>Crea un client con un proprio <see cref="HttpClient"/>, da smaltire con <see cref="Dispose"/>.</summary>
    public static TmdbClient CreateDefault(TmdbOptions? options = null) =>
        new(new HttpClient { Timeout = TimeSpan.FromSeconds(30) }, options ?? new TmdbOptions(), ownsHttp: true);

    /// <inheritdoc />
    public string Name => "tmdb";

    /// <summary>True quando è disponibile una chiave API.</summary>
    public bool IsConfigured => _options.IsConfigured;

    /// <summary>Cerca film per titolo e, quando indicato, per anno.</summary>
    public Task<IReadOnlyList<MediaCandidate>> SearchMovieAsync(string query, int? year = null, CancellationToken cancellationToken = default) =>
        SearchAsync(MediaKind.Movie, query, year, cancellationToken);

    /// <summary>Cerca serie TV per titolo e, quando indicato, per anno della prima messa in onda.</summary>
    public Task<IReadOnlyList<MediaCandidate>> SearchSeriesAsync(string query, int? year = null, CancellationToken cancellationToken = default) =>
        SearchAsync(MediaKind.Series, query, year, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<MediaCandidate>> SearchAsync(MediaKind kind, string query, int? year = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        if (!IsConfigured)
        {
            return [];
        }

        var (segment, yearParameter) = Endpoint(kind);
        var parameters = new List<(string Key, string Value)> { ("query", query) };
        if (year is > 0)
        {
            parameters.Add((yearParameter, year.Value.ToString(CultureInfo.InvariantCulture)));
        }

        using var response = await _http.GetAsync(BuildUrl($"/search/{segment}", parameters), cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<SearchPayload>(stream, JsonOptions, cancellationToken);
        if (payload?.Results is null)
        {
            return [];
        }

        var results = new List<MediaCandidate>(payload.Results.Count);
        foreach (var item in payload.Results)
        {
            var title = item.Title ?? item.Name;
            if (item.Id <= 0 || string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            results.Add(new MediaCandidate
            {
                Id = item.Id.ToString(CultureInfo.InvariantCulture),
                Kind = kind,
                Title = title,
                Year = ParseYear(item.ReleaseDate ?? item.FirstAirDate),
                Overview = item.Overview,
                PosterPath = item.PosterPath,
                BackdropPath = item.BackdropPath,
                VoteAverage = item.VoteAverage,
            });
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<MediaDetails?> DetailsAsync(MediaKind kind, string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!IsConfigured)
        {
            return null;
        }

        var (segment, _) = Endpoint(kind);
        using var response = await _http.GetAsync(BuildUrl($"/{segment}/{Uri.EscapeDataString(id.Trim())}"), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<DetailsPayload>(stream, JsonOptions, cancellationToken);
        var title = payload?.Title ?? payload?.Name;
        if (payload is null || string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var genres = payload.Genres?
            .Where(g => !string.IsNullOrWhiteSpace(g.Name))
            .Select(g => g.Name!)
            .ToList() ?? [];

        return new MediaDetails
        {
            Id = id.Trim(),
            Kind = kind,
            Title = title,
            Year = ParseYear(payload.ReleaseDate ?? payload.FirstAirDate),
            Overview = payload.Overview,
            PosterPath = payload.PosterPath,
            BackdropPath = payload.BackdropPath,
            VoteAverage = payload.VoteAverage,
            Genres = genres,
            RuntimeMinutes = payload.Runtime is > 0 ? payload.Runtime : payload.EpisodeRunTime?.FirstOrDefault(value => value > 0),
            SeasonCount = payload.NumberOfSeasons,
            EpisodeCount = payload.NumberOfEpisodes,
        };
    }

    /// <summary>Smaltisce l'<see cref="HttpClient"/> solo quando è stato creato da <see cref="CreateDefault"/>.</summary>
    public void Dispose()
    {
        if (_ownsHttp)
        {
            _http.Dispose();
        }
    }

    private static (string Segment, string YearParameter) Endpoint(MediaKind kind) => kind switch
    {
        MediaKind.Movie => ("movie", "year"),
        MediaKind.Series or MediaKind.Episode => ("tv", "first_air_date_year"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "TMDB espone solo film e serie."),
    };

    private static int? ParseYear(string? date) =>
        date is { Length: >= 4 } && int.TryParse(date.AsSpan(0, 4), CultureInfo.InvariantCulture, out var year) ? year : null;

    private string BuildUrl(string path, List<(string Key, string Value)>? query = null)
    {
        var builder = new StringBuilder(_options.BaseUrl.TrimEnd('/'));
        builder.Append(path);
        var separator = '?';

        foreach (var (key, value) in query ?? [])
        {
            builder.Append(separator).Append(key).Append('=').Append(Uri.EscapeDataString(value));
            separator = '&';
        }

        builder.Append(separator).Append("api_key=").Append(Uri.EscapeDataString(_options.ApiKey.Trim()));
        if (!string.IsNullOrWhiteSpace(_options.Language))
        {
            builder.Append('&').Append("language=").Append(Uri.EscapeDataString(_options.Language.Trim()));
        }

        return builder.ToString();
    }

    private sealed class SearchPayload
    {
        [JsonPropertyName("results")] public List<SearchItem>? Results { get; set; }
    }

    private sealed class SearchItem
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("overview")] public string? Overview { get; set; }
        [JsonPropertyName("poster_path")] public string? PosterPath { get; set; }
        [JsonPropertyName("backdrop_path")] public string? BackdropPath { get; set; }
        [JsonPropertyName("vote_average")] public double VoteAverage { get; set; }
        [JsonPropertyName("release_date")] public string? ReleaseDate { get; set; }
        [JsonPropertyName("first_air_date")] public string? FirstAirDate { get; set; }
    }

    private sealed class DetailsPayload
    {
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("overview")] public string? Overview { get; set; }
        [JsonPropertyName("poster_path")] public string? PosterPath { get; set; }
        [JsonPropertyName("backdrop_path")] public string? BackdropPath { get; set; }
        [JsonPropertyName("vote_average")] public double VoteAverage { get; set; }
        [JsonPropertyName("release_date")] public string? ReleaseDate { get; set; }
        [JsonPropertyName("first_air_date")] public string? FirstAirDate { get; set; }
        [JsonPropertyName("runtime")] public int? Runtime { get; set; }
        [JsonPropertyName("episode_run_time")] public List<int>? EpisodeRunTime { get; set; }
        [JsonPropertyName("number_of_seasons")] public int? NumberOfSeasons { get; set; }
        [JsonPropertyName("number_of_episodes")] public int? NumberOfEpisodes { get; set; }
        [JsonPropertyName("genres")] public List<GenreItem>? Genres { get; set; }
    }

    private sealed class GenreItem
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
    }
}
