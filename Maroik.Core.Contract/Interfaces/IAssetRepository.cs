using Maroik.Core.Domain.Finance;
namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Repository abstraction over <see cref="Asset"/> persistence.
/// </summary>
public interface IAssetRepository : IGenericRepository<Asset>
{
    /// <summary>
    /// Updates an asset, matching the existing row by its original product name
    /// (used when the update itself renames the product). Returns the number of rows affected.
    /// </summary>
    Task<int> UpdateAssetWithProductNameAsync(Asset asset, string originalProductName, CancellationToken ct = default);

    /// <summary>Returns all assets belonging to the given account.</summary>
    Task<List<Asset>> GetByAccountEmailAsync(string accountEmail, CancellationToken ct = default);

    /// <summary>
    /// Returns assets for the given account whose ProductName, Item, currency, Note, Amount,
    /// Created, Updated, or Deleted flag contains <paramref name="search"/> — filtered in the
    /// database instead of loading every record.
    /// </summary>
    Task<List<Asset>> SearchByAccountEmailAsync(string accountEmail, string search, CancellationToken ct = default);

    /// <summary>Returns the asset identified by account email and product name, or null if not found.</summary>
    Task<Asset?> FindByEmailAndProductNameAsync(string accountEmail, string productName, CancellationToken ct = default);

    /// <summary>
    /// Same lookup as <see cref="FindByEmailAndProductNameAsync"/> but takes a row-level
    /// database lock (SELECT ... FOR UPDATE). Within an active <see cref="IUnitOfWork"/>
    /// transaction the lock is held until commit/rollback, serializing concurrent
    /// balance mutations of the same asset (e.g. double-submitted requests). Deliberately
    /// returns a deleted asset too (not filtered) so callers can distinguish "doesn't exist"
    /// from "exists but deleted" and report the latter with a specific message.
    /// </summary>
    Task<Asset?> FindByEmailAndProductNameForUpdateAsync(string accountEmail, string productName, CancellationToken ct = default);

    /// <summary>
    /// Batched form of <see cref="FindByEmailAndProductNameForUpdateAsync"/>: row-locks (SELECT ...
    /// FOR UPDATE) every asset in <paramref name="productNames"/> for the given account in a single
    /// round trip, instead of one query per name. The single statement locks rows in its
    /// <c>ORDER BY "ProductName"</c> order, whatever order the names were passed in, so two callers
    /// batch-locking overlapping sets of assets always acquire them in the same order and cannot
    /// deadlock against each other. Names with no matching row are simply absent from the result.
    /// </summary>
    Task<List<Asset>> FindByEmailAndProductNamesForUpdateAsync(string accountEmail, IReadOnlyCollection<string> productNames, CancellationToken ct = default);
}
