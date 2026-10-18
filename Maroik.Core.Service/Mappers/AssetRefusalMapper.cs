using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Finances;

namespace Maroik.Core.Service.Mappers;

/// <summary>
/// Answers an <see cref="AssetUsagePolicy"/> refusal with the <see cref="ServiceResult"/> the Finance services
/// return for it. The error code belongs to the record type (<see cref="Codes"/>, written out in full so a code can
/// be found by searching for it); the message key is the same for every record type.
/// </summary>
internal static class AssetRefusalMapper
{
    /// <summary>The error codes of one record type, one per <see cref="AssetRefusal"/> it can be refused with.</summary>
    /// <param name="AssetNotFound">Code of <see cref="AssetRefusal.NotFound"/>.</param>
    /// <param name="AssetDeleted">Code of <see cref="AssetRefusal.Deleted"/>.</param>
    /// <param name="CurrencyMismatch">
    /// Code of <see cref="AssetRefusal.CurrencyMismatch"/>; none for a record type that names a single asset (an
    /// income), which can never be refused for it.
    /// </param>
    internal sealed record Codes(string AssetNotFound, string AssetDeleted, string? CurrencyMismatch = null);

    /// <summary>The codes of an income.</summary>
    internal static readonly Codes Income = new("Income.AssetNotFound", "Income.AssetDeleted");

    /// <summary>The codes of an expenditure.</summary>
    internal static readonly Codes Expenditure = new("Expenditure.AssetNotFound", "Expenditure.AssetDeleted", "Expenditure.CurrencyMismatch");

    /// <summary>The codes of a fixed income.</summary>
    internal static readonly Codes FixedIncome = new("FixedIncome.AssetNotFound", "FixedIncome.AssetDeleted");

    /// <summary>The codes of a fixed expenditure.</summary>
    internal static readonly Codes FixedExpenditure = new("FixedExpenditure.AssetNotFound", "FixedExpenditure.AssetDeleted", "FixedExpenditure.CurrencyMismatch");

    /// <summary>
    /// The failure for <paramref name="refusal"/> under <paramref name="codes"/>. No refusal (a check that passed), a
    /// refusal the record type has no code for, or a value outside <see cref="AssetRefusal"/> throws.
    /// </summary>
    internal static ServiceResult ToResult(Codes codes, AssetRefusal? refusal) => refusal switch
    {
        AssetRefusal.NotFound => ServiceResult.NotFound(codes.AssetNotFound, ServiceErrorKeys.SelectedAssetNotFound),
        AssetRefusal.Deleted => ServiceResult.Conflict(codes.AssetDeleted, ServiceErrorKeys.AssetAlreadyDeleted),
        AssetRefusal.CurrencyMismatch when codes.CurrencyMismatch is { } code => ServiceResult.Validation(code, ServiceErrorKeys.CurrencyMismatch),
        _ => throw new ArgumentOutOfRangeException(nameof(refusal), refusal, "No result for this refusal."),
    };
}
