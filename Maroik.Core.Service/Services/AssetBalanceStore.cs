using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Finance;

namespace Maroik.Core.Service.Services;

/// <summary>
/// Thin delegating wrapper that exposes only the asset-balance operations
/// needed by Finance transaction services, keeping them free of a direct
/// <see cref="IAssetRepository"/> dependency.
/// </summary>
public class AssetBalanceStore(IAssetRepository assetRepository) : IAssetBalanceStore
{
    /// <inheritdoc />
    /// <remarks>
    /// Delegates to the row-locking lookup so that concurrent balance mutations of the
    /// same asset serialize inside the ambient unit-of-work transaction (lost-update guard).
    /// </remarks>
    public Task<Asset?> GetAssetAsync(string accountEmail, string productName, CancellationToken ct = default)
        => assetRepository.FindByEmailAndProductNameForUpdateAsync(accountEmail, productName, ct);

    /// <inheritdoc />
    public async Task<Dictionary<string, Asset>> GetAssetsAsync(string accountEmail, IReadOnlyCollection<string> productNames, CancellationToken ct = default)
    {
        List<Asset> assets = await assetRepository.FindByEmailAndProductNamesForUpdateAsync(accountEmail, productNames, ct);
        return assets.ToDictionary(a => a.ProductName, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public Task<Asset?> GetAssetForReadAsync(string accountEmail, string productName, CancellationToken ct = default)
        => assetRepository.FindByEmailAndProductNameAsync(accountEmail, productName, ct);

    /// <inheritdoc />
    public Task SaveAsync(Asset asset, CancellationToken ct = default)
        => assetRepository.UpdateEntityAsync(asset, ct);
}
