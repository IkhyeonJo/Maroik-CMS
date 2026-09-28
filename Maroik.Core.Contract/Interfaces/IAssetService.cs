using Maroik.Core.Contract.Dtos;

namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Service interface for asset management business logic.
/// </summary>
public interface IAssetService
{
    /// <summary>Returns all active assets for the given account.</summary>
    Task<List<AssetResponse>> GetAssetsAsync(string accountEmail, CancellationToken ct = default);

    /// <summary>Returns assets for the given account whose fields contain <paramref name="search"/>, filtered in the database.</summary>
    Task<List<AssetResponse>> SearchAssetsAsync(string accountEmail, string search, CancellationToken ct = default);

    /// <summary>Returns a single asset by product name, or null.</summary>
    Task<AssetResponse?> GetAssetAsync(string accountEmail, string productName, CancellationToken ct = default);

    /// <summary>Validates and creates a new asset for the given account.</summary>
    Task<ServiceResult> CreateAsync(string accountEmail, AssetRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates an asset. If the product name changed, cascades the rename to linked
    /// income and expenditure records.
    /// </summary>
    Task<ServiceResult> UpdateAsync(string accountEmail, AssetRequest request, string originalProductName, CancellationToken ct = default);

    /// <summary>Soft-deletes an asset (sets Deleted = true).</summary>
    Task<ServiceResult> DeleteAsync(string accountEmail, string productName, CancellationToken ct = default);
}
