using System.Diagnostics.CodeAnalysis;

namespace Maroik.Core.Domain.Finance;

/// <summary>
/// Which assets an income / expenditure record (one-off or fixed) may use. A <see langword="null"/> asset means the
/// name did not resolve to an asset of the caller's account. Each check returns <see langword="true"/> when the
/// assets are acceptable; otherwise <see langword="false"/> with the first refusal in this order: per asset, missing
/// before deleted; the payment (or required) asset before a transfer's deposit asset; the currency match last among
/// the new assets; the previous assets of an edit after all of the new ones. The refusal means nothing on success.
/// </summary>
public static class AssetUsagePolicy
{
    /// <summary>An asset the record must use: refused when it is missing, then when it is deleted.</summary>
    public static bool CanUse([NotNullWhen(true)] Asset? asset, out AssetRefusal refusal)
    {
        refusal = asset is null ? AssetRefusal.NotFound : AssetRefusal.Deleted;
        return asset is { Deleted: false };
    }

    /// <summary>
    /// The deposit asset of a transfer paid from <paramref name="payment"/>: refused when it is missing, when it is
    /// deleted, then when its currency label differs from the payment asset's.
    /// </summary>
    public static bool CanTransferInto(Asset payment, [NotNullWhen(true)] Asset? deposit, out AssetRefusal refusal)
    {
        if (!CanUse(deposit, out refusal)) return false;

        refusal = AssetRefusal.CurrencyMismatch;
        return payment.Balance.Currency == deposit.Balance.Currency;
    }

    /// <summary>
    /// Assets whose balance a record already moved and will move back (an edit or a delete): refused when any of them
    /// is deleted. A missing asset is not refused here.
    /// </summary>
    public static bool CanMoveBack(IEnumerable<Asset?> assets, out AssetRefusal refusal)
    {
        refusal = AssetRefusal.Deleted;
        return !assets.Any(asset => asset is { Deleted: true });
    }

    /// <summary>
    /// A new expenditure: the payment asset is required (<see cref="CanUse"/>); a transfer also needs its deposit
    /// asset (<see cref="CanTransferInto"/>). A non-transfer's <paramref name="deposit"/> is ignored.
    /// </summary>
    public static bool CanRecordExpenditure(bool isTransfer, [NotNullWhen(true)] Asset? payment, Asset? deposit, out AssetRefusal refusal)
        => CanUse(payment, out refusal) && (!isTransfer || CanTransferInto(payment, deposit, out refusal));

    /// <summary>
    /// An edited expenditure: its new assets are held to <see cref="CanRecordExpenditure"/>, then its previous
    /// payment / deposit assets to <see cref="CanMoveBack"/>.
    /// </summary>
    public static bool CanRetargetExpenditure(
        bool isTransfer, [NotNullWhen(true)] Asset? payment, Asset? deposit, Asset? previousPayment, Asset? previousDeposit,
        out AssetRefusal refusal)
        => CanRecordExpenditure(isTransfer, payment, deposit, out refusal)
           && CanMoveBack([previousPayment, previousDeposit], out refusal);

    /// <summary>
    /// An edited income: its previous deposit asset is held to <see cref="CanMoveBack"/>, then a named new deposit
    /// asset to <see cref="CanUse"/>. An empty <paramref name="nextName"/> is left to the income's own validation.
    /// </summary>
    public static bool CanRetargetIncome(Asset? previous, string? nextName, Asset? next, out AssetRefusal refusal)
        => CanMoveBack([previous], out refusal)
           && (string.IsNullOrEmpty(nextName) || CanUse(next, out refusal));
}
