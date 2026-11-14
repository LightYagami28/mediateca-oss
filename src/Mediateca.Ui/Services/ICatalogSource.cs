using Mediateca.Ui.Models;

namespace Mediateca.Ui.Services;

public interface ICatalogSource
{
    Task<IReadOnlyList<CatalogRow>> LoadAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CatalogRow>> SearchAsync(string query, int limit, CancellationToken cancellationToken = default);
}
