using Mediateca.Core.Domain;
using Mediateca.Data;
using Mediateca.Telegram;

namespace Mediateca.Ui.Services;

public sealed class BotCatalogAdapter(MediatecaDatabase database) : IBotCatalog
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

public sealed class BotStatisticsAdapter(MediatecaDatabase database) : IBotStatistics
{
    private readonly SqliteMediaStore _store = new(database);

    public Task<BotStats> ReadAsync(CancellationToken cancellationToken = default)
    {
        var (items, bytes, channels) = _store.Statistics();
        return Task.FromResult(new BotStats(items, bytes, channels));
    }
}

public sealed class BotUserDirectoryAdapter(MediatecaDatabase database, IEnumerable<long> bootstrapAdmins) : IBotUserDirectory
{
    private readonly BotUserStore _store = new(database, bootstrapAdmins);

    public Task<BotUser> TouchAsync(long userId, string? username, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.Touch(userId, username));

    public Task<bool> SetRoleAsync(long userId, BotUserRole role, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.SetRole(userId, role));

    public Task<IReadOnlyList<BotUser>> ListAsync(int limit, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.List(limit));
}
