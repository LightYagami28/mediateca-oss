using Mediateca.Core.Domain;

namespace Mediateca.Media;

/// <summary>
/// Provider di metadati interrogabile per arricchire il catalogo.
/// </summary>
/// <remarks>
/// L'interfaccia vive qui e non in <c>Mediateca.Core</c>: descrive il contratto dei soli provider
/// di metadati, che è un dettaglio di questo progetto. Un'implementazione reale è
/// <see cref="TmdbClient"/>.
/// </remarks>
public interface IMediaScraper
{
    /// <summary>Nome breve del provider, usato dal registro per risolverlo.</summary>
    string Name { get; }

    /// <summary>True quando il provider ha tutto il necessario per rispondere (per esempio una chiave API).</summary>
    bool IsConfigured { get; }

    /// <summary>Cerca titoli del tipo indicato.</summary>
    /// <param name="kind">Tipo di contenuto, per esempio <see cref="MediaKind.Movie"/>.</param>
    /// <param name="query">Testo cercato.</param>
    /// <param name="year">Anno di riferimento, quando noto.</param>
    /// <param name="cancellationToken">Annullamento della chiamata.</param>
    Task<IReadOnlyList<MediaCandidate>> SearchAsync(MediaKind kind, string query, int? year = null, CancellationToken cancellationToken = default);

    /// <summary>Legge la scheda completa di un titolo, o <c>null</c> se il provider non lo conosce.</summary>
    /// <param name="kind">Tipo di contenuto.</param>
    /// <param name="id">Identificativo del titolo presso il provider.</param>
    /// <param name="cancellationToken">Annullamento della chiamata.</param>
    Task<MediaDetails?> DetailsAsync(MediaKind kind, string id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Registro dei provider di metadati, con risoluzione per nome e ripiego.
/// </summary>
/// <remarks>
/// La risoluzione procede così: il nome richiesto se registrato, altrimenti <see cref="FallbackName"/>,
/// altrimenti il primo provider registrato. Il registro non è thread-safe in scrittura: si popola
/// all'avvio e poi si legge.
/// </remarks>
public sealed class ScraperRegistry
{
    private readonly Dictionary<string, IMediaScraper> _scrapers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Nome del provider usato quando quello richiesto non è registrato.</summary>
    public string? FallbackName { get; set; } = "tmdb";

    /// <summary>Numero di provider registrati.</summary>
    public int Count => _scrapers.Count;

    /// <summary>Nomi dei provider registrati, nell'ordine di inserimento.</summary>
    public IReadOnlyCollection<string> ProviderNames => _scrapers.Keys;

    /// <summary>Restituisce i provider registrati in una nuova lista, senza esporre la collezione interna.</summary>
    public IReadOnlyList<IMediaScraper> GetScrapers() => _scrapers.Values.ToList();

    /// <summary>Registra un provider; a parità di nome l'ultimo vince.</summary>
    public void Register(IMediaScraper scraper)
    {
        if (scraper != null)
        {
            ArgumentNullException.ThrowIfNull(scraper);
            ArgumentException.ThrowIfNullOrWhiteSpace(scraper.Name);
            _scrapers[scraper.Name] = scraper;
        }
        else
        {
            throw new ArgumentNullException();
        }
    }

    /// <summary>Rimuove un provider per nome, indicando se esisteva.</summary>
    public bool Unregister(string name) =>
        !string.IsNullOrWhiteSpace(name) && _scrapers.Remove(name.Trim());

    /// <summary>True quando esiste un provider con questo nome.</summary>
    public bool Contains(string? name) =>
        !string.IsNullOrWhiteSpace(name) && _scrapers.ContainsKey(name.Trim());

    /// <summary>
    /// Risolve il provider da usare: nome richiesto, poi ripiego, poi primo registrato.
    /// </summary>
    /// <returns>Il provider, oppure <c>null</c> se il registro è vuoto.</returns>
    public IMediaScraper? Resolve(string? providerName)
    {
        if (!string.IsNullOrWhiteSpace(providerName) && _scrapers.TryGetValue(providerName.Trim(), out var named))
        {
            return named;
        }

        if (FallbackName is { Length: > 0 } fallback && _scrapers.TryGetValue(fallback, out var fallbackScraper))
        {
            return fallbackScraper;
        }

        return _scrapers.Values.FirstOrDefault();
    }

    /// <summary>Primo provider configurato, usato come ripiego quando i nominati non hanno le credenziali.</summary>
    public IMediaScraper? FirstConfigured() => _scrapers.Values.FirstOrDefault(scraper => scraper.IsConfigured);
}
