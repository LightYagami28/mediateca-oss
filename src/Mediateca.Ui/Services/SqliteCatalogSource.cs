using Mediateca.Core.Domain;
using Mediateca.Data;
using Mediateca.Ui.Models;

namespace Mediateca.Ui.Services;

public sealed class SqliteCatalogSource(MediatecaDatabase database) : ICatalogSource
{
    private readonly SqliteMediaStore _store = new(database);
    private IReadOnlyDictionary<long, string> _channels = new Dictionary<long, string>();

    public Task<IReadOnlyList<CatalogRow>> LoadAsync(CancellationToken cancellationToken = default)
    {
        database.Migrate();
        _channels = LoadChannels();
        var rows = _store.Search("", null, 100_000)
            .Select(item => ToRow(item))
            .ToList();
        return Task.FromResult<IReadOnlyList<CatalogRow>>(rows);
    }

    public Task<IReadOnlyList<CatalogRow>> SearchAsync(string query, int limit, CancellationToken cancellationToken = default)
    {
        database.Migrate();
        if (_channels.Count == 0)
        {
            _channels = LoadChannels();
        }

        var rows = _store.Search(query.Trim(), null, Math.Clamp(limit, 1, 10_000))
            .Select(item => ToRow(item))
            .ToList();
        return Task.FromResult<IReadOnlyList<CatalogRow>>(rows);
    }

    private IReadOnlyDictionary<long, string> LoadChannels() =>
        _store.ListChannels().ToDictionary(channel => channel.Id, channel => channel.Title);

    private CatalogRow ToRow(MediaItem item) => new()
    {
        Id = item.Id,
        Name = item.Name,
        Extension = item.Extension,
        KindLabel = KindStyle.Label(item.Kind),
        KindColor = KindStyle.Color(item.Kind),
        Channel = _channels.TryGetValue(item.ChannelId, out var title) ? title : "—",
        Size = item.Size,
        DurationSeconds = item.DurationSeconds,
        Year = item.Year,
        UpdatedAt = item.UpdatedAt,
    };
}
