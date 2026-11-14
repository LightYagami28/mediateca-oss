using Teledisk.Ui.Models;

namespace Teledisk.Ui.Services;

public interface ICatalogSource
{
    Task<IReadOnlyList<CatalogRow>> LoadAsync(CancellationToken cancellationToken = default);
}