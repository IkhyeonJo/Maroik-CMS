using ErrorOr;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Finance;
using Maroik.Core.Domain.ValueObjects;

namespace Maroik.Core.Service.Extensions;

/// <summary>
/// A single asset-balance adjustment to apply as part of reverting or re-applying the effect
/// a transaction record (income, expenditure, ...) had on an asset's balance.
/// </summary>
/// <param name="AssetName">Asset product name; empty/null adjustments are skipped.</param>
/// <param name="Amount">Amount to deposit or withdraw (sign is ignored — <see cref="IsDeposit"/> controls direction).</param>
/// <param name="IsDeposit">true to deposit into the asset, false to withdraw from it.</param>
/// <param name="Currency">
/// Currency label of the transaction row being reverted or re-applied. Rows carry no currency of their
/// own — the repository reads it from the asset they reference — so it normally equals the asset's
/// label. It is still passed through as a defensive check: if the two ever differ (e.g. the two assets
/// of an older transfer were relabeled differently), the deposit/withdraw is rejected with a
/// currency-mismatch error instead of silently drifting the balance. Null falls back to the asset's
/// current currency.
/// </param>
public readonly record struct AssetBalanceAdjustment(string? AssetName, decimal Amount, bool IsDeposit, string? Currency = null);

/// <summary>
/// Extension methods for locking multiple <see cref="Asset"/> rows through
/// <see cref="IAssetBalanceDomainService"/> in a deadlock-safe order.
/// </summary>
public static class AssetBalanceOrdinalLockExtensions
{
    extension(IAssetBalanceDomainService assetBalance)
    {
        /// <summary>
        /// Fetches (and row-locks via <see cref="IAssetBalanceDomainService.GetAssetsAsync"/>, in a
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
        /// Batch-fetches (via <see cref="GetAssetsOrdinalAsync"/>) every asset referenced by
        /// <paramref name="adjustments"/>, applies each deposit/withdrawal in order, then saves every
        /// touched asset. Used to revert a transaction record's old asset impact and/or apply its new
        /// one from a single fetched snapshot, avoiding duplicate reads of the same asset.
        /// A caller that already holds a dictionary covering these asset names — typically because it
        /// fetched one of its own for validation just before calling this — should use the
        /// <see cref="ApplyAssetBalanceAdjustmentsAsync(IAssetBalanceDomainService, Dictionary{string, Asset}, IEnumerable{AssetBalanceAdjustment}, string, string, CancellationToken)"/>
        /// overload instead, to reuse that fetch rather than repeating it here.
        /// </summary>
        /// <param name="accountEmail">Owner of every asset named in <paramref name="adjustments"/>.</param>
        /// <param name="adjustments">Adjustments to apply; entries with an empty <see cref="AssetBalanceAdjustment.AssetName"/> are skipped.</param>
        /// <param name="notFoundErrorCode">Error code used when a non-empty asset name does not resolve to an asset.</param>
        /// <param name="notFoundErrorMessage">Error message used when a non-empty asset name does not resolve to an asset.</param>
        /// <param name="ct">Cancellation token.</param>
        public async Task<ErrorOr<Success>> ApplyAssetBalanceAdjustmentsAsync(
            string accountEmail,
            IEnumerable<AssetBalanceAdjustment> adjustments,
            string notFoundErrorCode,
            string notFoundErrorMessage,
            CancellationToken ct = default)
        {
            List<AssetBalanceAdjustment> toApply = [.. adjustments.Where(a => !string.IsNullOrEmpty(a.AssetName))];
            Dictionary<string, Asset> assets = await assetBalance.GetAssetsOrdinalAsync(
                accountEmail, toApply.Select(a => a.AssetName), ct);

            return await assetBalance.ApplyAssetBalanceAdjustmentsAsync(
                assets, toApply, notFoundErrorCode, notFoundErrorMessage, ct);
        }

        /// <summary>
        /// Applies each deposit/withdrawal in <paramref name="adjustments"/> against the already-fetched
        /// <paramref name="assets"/> (from a prior <see cref="GetAssetsOrdinalAsync"/> call — typically
        /// one the caller made for its own pre-write validation), then saves every touched asset,
        /// without fetching the assets a second time.
        /// </summary>
        /// <param name="assets">Already row-locked assets, keyed by name, covering every name in <paramref name="adjustments"/>.</param>
        /// <param name="adjustments">Adjustments to apply; entries with an empty <see cref="AssetBalanceAdjustment.AssetName"/> are skipped.</param>
        /// <param name="notFoundErrorCode">Error code used when a non-empty asset name does not resolve to an asset.</param>
        /// <param name="notFoundErrorMessage">Error message used when a non-empty asset name does not resolve to an asset.</param>
        /// <param name="ct">Cancellation token.</param>
        public async Task<ErrorOr<Success>> ApplyAssetBalanceAdjustmentsAsync(
            Dictionary<string, Asset> assets,
            IEnumerable<AssetBalanceAdjustment> adjustments,
            string notFoundErrorCode,
            string notFoundErrorMessage,
            CancellationToken ct = default)
        {
            List<AssetBalanceAdjustment> toApply = [.. adjustments.Where(a => !string.IsNullOrEmpty(a.AssetName))];
            HashSet<string> touchedNames = [];

            foreach (var adjustment in toApply)
            {
                if (!assets.TryGetValue(adjustment.AssetName!, out var asset))
                    return Error.NotFound(notFoundErrorCode, notFoundErrorMessage);

                Money amount;
                if (string.IsNullOrEmpty(adjustment.Currency))
                {
                    amount = asset.Balance.WithAmount(Math.Abs(adjustment.Amount));
                }
                else
                {
                    var moneyResult = Money.Create(Math.Abs(adjustment.Amount), adjustment.Currency);
                    if (moneyResult.IsError) return moneyResult.Errors;
                    amount = moneyResult.Value;
                }

                var result = adjustment.IsDeposit ? asset.Deposit(amount) : asset.Withdraw(amount);
                if (result.IsError) return result.Errors;

                touchedNames.Add(adjustment.AssetName!);
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
