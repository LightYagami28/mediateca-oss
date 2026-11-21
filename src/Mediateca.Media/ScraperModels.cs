using Mediateca.Core.Domain;

namespace Mediateca.Media;

/// <summary>
/// Risultato sintetico di una ricerca su un provider di metadati.
/// </summary>
/// <remarks>
/// Il candidato è pensato per la lista dei risultati: contiene solo ciò che serve a mostrare una
/// riga e a chiedere il dettaglio, senza caricare il payload completo del provider.
/// </remarks>
public sealed record MediaCandidate
{
    /// <summary>Identificativo del titolo presso il provider.</summary>
    public required string Id { get; init; }

    /// <summary>Tipo di contenuto.</summary>
    public MediaKind Kind { get; init; } = MediaKind.Unknown;

    /// <summary>Titolo leggibile.</summary>
    public required string Title { get; init; }

    /// <summary>Anno di uscita, quando noto.</summary>
    public int? Year { get; init; }

    /// <summary>Trama breve.</summary>
    public string? Overview { get; init; }

    /// <summary>Percorso relativo della locandina presso il provider.</summary>
    public string? PosterPath { get; init; }

    /// <summary>Percorso relativo dello sfondo presso il provider.</summary>
    public string? BackdropPath { get; init; }

    /// <summary>Voto medio su scala 0-10.</summary>
    public double VoteAverage { get; init; }

    /// <summary>URL completo della locandina, quando il provider la espone.</summary>
    public string? PosterUrl => TmdbImages.Poster(PosterPath);

    /// <summary>URL completo dello sfondo, quando il provider lo espone.</summary>
    public string? BackdropUrl => TmdbImages.Backdrop(BackdropPath);
}

/// <summary>
/// Scheda completa di un titolo, usata dal dettaglio della libreria.
/// </summary>
public sealed record MediaDetails
{
    /// <summary>Identificativo del titolo presso il provider.</summary>
    public required string Id { get; init; }

    /// <summary>Tipo di contenuto.</summary>
    public MediaKind Kind { get; init; } = MediaKind.Unknown;

    /// <summary>Titolo leggibile.</summary>
    public required string Title { get; init; }

    /// <summary>Anno di uscita, quando noto.</summary>
    public int? Year { get; init; }

    /// <summary>Trama.</summary>
    public string? Overview { get; init; }

    /// <summary>Percorso relativo della locandina presso il provider.</summary>
    public string? PosterPath { get; init; }

    /// <summary>Percorso relativo dello sfondo presso il provider.</summary>
    public string? BackdropPath { get; init; }

    /// <summary>Voto medio su scala 0-10.</summary>
    public double VoteAverage { get; init; }

    /// <summary>Generi normalizzati.</summary>
    public IReadOnlyList<string> Genres { get; init; } = [];

    /// <summary>Durata in minuti per film ed episodi.</summary>
    public int? RuntimeMinutes { get; init; }

    /// <summary>Numero di stagioni per le serie.</summary>
    public int? SeasonCount { get; init; }

    /// <summary>Numero di episodi per le serie.</summary>
    public int? EpisodeCount { get; init; }

    /// <summary>URL completo della locandina, quando il provider la espone.</summary>
    public string? PosterUrl => TmdbImages.Poster(PosterPath);

    /// <summary>URL completo dello sfondo, quando il provider lo espone.</summary>
    public string? BackdropUrl => TmdbImages.Backdrop(BackdropPath);
}

/// <summary>
/// Costruzione degli URL delle immagini del servizio TMDB.
/// </summary>
public static class TmdbImages
{
    /// <summary>Host del servizio immagini.</summary>
    public const string ImageHost = "image.tmdb.org";

    /// <summary>Radice del servizio immagini, composta senza URI assoluti hardcoded.</summary>
    public static string BaseUrl { get; } = new UriBuilder(Uri.UriSchemeHttps, ImageHost, -1, "/t/p").Uri.ToString().TrimEnd('/');

    /// <summary>Larghezza predefinita delle locandine.</summary>
    public const string PosterSize = "w500";

    /// <summary>Larghezza predefinita degli sfondi.</summary>
    public const string BackdropSize = "w780";

    /// <summary>URL completo di una locandina, o <c>null</c> se il percorso è assente.</summary>
    public static string? Poster(string? path, string size = PosterSize) => Build(path, size);

    /// <summary>URL completo di uno sfondo, o <c>null</c> se il percorso è assente.</summary>
    public static string? Backdrop(string? path, string size = BackdropSize) => Build(path, size);

    /// <summary>Compone l'URL a partire dal percorso relativo restituito dal provider.</summary>
    /// <param name="path">Percorso relativo del provider, con o senza barra iniziale.</param>
    /// <param name="size">Variante di larghezza richiesta dal servizio immagini.</param>
    public static string? Build(string? path, string size = PosterSize)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(size))
        {
            return null;
        }

        var normalized = path.StartsWith('/') ? path : "/" + path;
        return $"{BaseUrl}/{size}{normalized}";
    }
}
