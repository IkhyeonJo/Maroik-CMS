using ErrorOr;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Finances;

namespace Maroik.Core.Service.Extensions;

/// <summary>
/// Extension methods for locking multiple <see cref="Asset"/> rows through
/// <see cref="IAssetBalanceStore"/> in a deadlock-safe order.
/// </summary>
public static class AssetBalanceOrdinalLockExtensions
{
    extension(Dictionary<string, Asset> assets)
    {
        /// <summary>
        /// The asset named <paramref name="productName"/> among these (row-locked) assets; null when the name is empty
        /// or not among them — the "missing asset" the <see cref="AssetUsagePolicy"/> checks expect.
        /// </summary>
        public Asset? FindAsset(string? productName) =>
            !string.IsNullOrEmpty(productName) && assets.TryGetValue(productName, out var asset) ? asset : null;
    }

    extension(IAssetBalanceStore assetBalance)
    {
        /// <summary>
        /// Fetches (and row-locks via <see cref="IAssetBalanceStore.GetAssetsAsync"/>, in a
        /// single batched round trip) every distinct, non-empty asset name in
        /// <paramref name="productNames"/>, always in ordinal name order. Locking in a single,
        /// name-based order regardless of the caller's role for each asset (payment vs. deposit,
        /// previous vs. new, ...) ensures every transaction that touches the same pair of assets
        /// acquires their locks in the same order, so concurrent transactions cannot deadlock on
        /// each other.
        /// </summary>
        public async Task<Dictionary<string, Asset>> GetAssetsOrdinalAsync(
            string accountEmail,
            IEnumerable<string?> productNames,
            CancellationToken ct = default)
        {
            List<string> names = [.. productNames
                .Where(n => !string.IsNullOrEmpty(n))
                .Select(n => n!)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(n => n, StringComparer.Ordinal)];

            return await assetBalance.GetAssetsAsync(accountEmail, names, ct);
        }

        /// <summary>
        /// Carries out each of <paramref name="movements"/> (<see cref="Asset.Apply"/>) in order against the
        /// already-fetched <paramref name="assets"/> (from a prior <see cref="GetAssetsOrdinalAsync"/> call — the one
        /// the caller made for its own pre-write validation), then saves every touched asset, without fetching the
        /// assets a second time. Nothing is saved unless every movement succeeds.
        /// </summary>
        /// <param name="assets">
        /// Already row-locked assets, keyed by name, covering every name in <paramref name="movements"/>: a new asset
        /// passed <see cref="AssetUsagePolicy"/>, and a stored record's assets exist by its foreign keys (an asset is only
        /// ever soft-deleted) and were locked with the rest. A name outside them is a bug, not a refusal, and throws.
        /// </param>
        /// <param name="movements">Movements to carry out; one with an empty <see cref="BalanceMovement.AssetName"/> is skipped.</param>
        /// <param name="utcNow">The current UTC time, stamped on every adjusted asset.</param>
        /// <param name="ct">Cancellation token.</param>
        public async Task<ErrorOr<Success>> ApplyBalanceMovementsAsync(
            Dictionary<string, Asset> assets,
            IEnumerable<BalanceMovement> movements,
            DateTime utcNow,
            CancellationToken ct = default)
        {
            HashSet<string> touchedNames = [];

            foreach (var movement in movements.Where(m => !string.IsNullOrEmpty(m.AssetName)))
            {
                var result = assets[movement.AssetName].Apply(movement, utcNow);
                if (result.IsError) return result.Errors;

                touchedNames.Add(movement.AssetName);
            }

            // Only save assets this call actually adjusted — `assets` may hold extra entries the
            // caller fetched for its own validation (e.g. a since-archived asset) that this call
            // never touched.
            foreach (var name in touchedNames)
                await assetBalance.SaveAsync(assets[name], ct);

            return Result.Success;
        }
    }
}
