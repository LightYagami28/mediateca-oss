using Teledisk.Core.Domain;
using Teledisk.Data;
using Teledisk.Telegram;

namespace Teledisk.Ui.Services;

public sealed class BotCatalogAdapter(TelediskDatabase database) : IBotCatalog
{
    private readonly SqliteMediaStore _store = new(database);

    public Task<IReadOnlyList<CatalogHit>> SearchAsync(string query, int limit, CancellationToken cancellationToken = default)
    {
        var hits = _store.Search(query.Trim(), null, Math.Clamp(limit, 1, 50))
            .Select(item => new CatalogHit
            {
                Title = item.Name,
                SizeBytes = item.Size,
                Kind = item.Kind switch
                {
                    MediaKind.Movie => "Film",
                    MediaKind.Series => "Serie",
                    MediaKind.Episode => "Episodio",
                    MediaKind.Music => "Musica",
                    MediaKind.Book => "Libro",
                    MediaKind.Document => "Documento",
                    MediaKind.Image => "Immagine",
                    MediaKind.Archive => "Archivio",
                    _ => "Altro",
                },
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<CatalogHit>>(hits);
    }
}

public sealed class BotStatisticsAdapter(TelediskDatabase database) : IBotStatistics
{
    private readonly SqliteMediaStore _store = new(database);

    public Task<BotStats> ReadAsync(CancellationToken cancellationToken = default)
    {
        var (items, bytes, channels) = _store.Statistics();
        return Task.FromResult(new BotStats(items, bytes, channels));
    }
}
