using Maroik.Core.Domain.Finance;

namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Domain service abstraction for reading and persisting <see cref="Asset"/> balance state.
/// Shields Finance transaction services from a direct repository dependency.
/// </summary>
public interface IAssetBalanceDomainService
{
    /// <summary>
    /// Returns the asset identified by the composite key (email + productName), or null if not found.
    /// The row is read with a database lock (SELECT ... FOR UPDATE): within an active
    /// <see cref="IUnitOfWork"/> transaction the asset is protected from concurrent balance
    /// mutations until commit/rollback, so double-submitted requests serialize instead of
    /// losing an update. Outside a transaction the lock is released at end of statement.
    /// </summary>
    Task<Asset?> GetAssetAsync(string accountEmail, string productName, CancellationToken ct = default);

    /// <summary>
    /// Batched form of <see cref="GetAssetAsync"/>: row-locks and returns every asset in
    /// <paramref name="productNames"/> for the given account in a single round trip, keyed by
    /// product name. A name with no matching asset is simply absent from the result.
    /// </summary>
    Task<Dictionary<string, Asset>> GetAssetsAsync(string accountEmail, IReadOnlyCollection<string> productNames, CancellationToken ct = default);

    /// <summary>
    /// Returns the asset identified by the composite key (email + productName), or null if not found,
    /// <b>without</b> taking a row lock. Use this from read-only / schedule flows (e.g. fixed
    /// income/expenditure) that never mutate a balance and run outside an <see cref="IUnitOfWork"/>
    /// transaction — there, a <c>SELECT ... FOR UPDATE</c> would acquire a lock that is released
    /// immediately at end of statement, so it buys nothing and only adds contention.
    /// </summary>
    Task<Asset?> GetAssetForReadAsync(string accountEmail, string productName, CancellationToken ct = default);

    /// <summary>Persists changes to the given asset (insert or update).</summary>
    Task SaveAsync(Asset asset, CancellationToken ct = default);
}
