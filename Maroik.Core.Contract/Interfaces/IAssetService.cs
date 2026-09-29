using Maroik.Core.Contract.Dtos;

namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Service interface for asset management business logic.
/// </summary>
public interface IAssetService
{
    /// <summary>Returns every asset of the given account, soft-deleted ones included (check <c>Deleted</c> to hide them).</summary>
    Task<List<AssetResponse>> GetAssetsAsync(string accountEmail, CancellationToken ct = default);

    /// <summary>Returns assets for the given account whose fields contain <paramref name="search"/>, filtered in the database.</summary>
    Task<List<AssetResponse>> SearchAssetsAsync(string accountEmail, string search, CancellationToken ct = default);

    /// <summary>Returns a single asset by product name, or null.</summary>
    Task<AssetResponse?> GetAssetAsync(string accountEmail, string productName, CancellationToken ct = default);

    /// <summary>Validates and creates a new asset for the given account.</summary>
    Task<ServiceResult> CreateAsync(string accountEmail, AssetRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates an asset. If the product name changed, the rename reaches every linked income,
    /// expenditure, fixed-income and fixed-expenditure record through the database's
    /// <c>ON UPDATE CASCADE</c> foreign keys.
    /// </summary>
    Task<ServiceResult> UpdateAsync(string accountEmail, AssetRequest request, string originalProductName, CancellationToken ct = default);

    /// <summary>Soft-deletes an asset (sets Deleted = true).</summary>
    Task<ServiceResult> DeleteAsync(string accountEmail, string productName, CancellationToken ct = default);
}
