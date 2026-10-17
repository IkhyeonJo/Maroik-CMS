using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Finance;

namespace Maroik.Core.Service.Mappers;

/// <summary>
/// Answers an <see cref="AssetUsagePolicy"/> refusal with the <see cref="ServiceResult"/> the Finance services
/// return for it: the error code names the record type (<c>"Expenditure.AssetDeleted"</c>, ...), the message key is
/// the same for every record type.
/// </summary>
internal static class AssetRefusalMapper
{
    /// <summary>
    /// The failure for <paramref name="refusal"/> of a <paramref name="recordType"/> record (<c>Income</c>,
    /// <c>Expenditure</c>, <c>FixedIncome</c>, <c>FixedExpenditure</c>). A value outside <see cref="AssetRefusal"/> throws.
    /// </summary>
    internal static ServiceResult ToResult(string recordType, AssetRefusal refusal) => refusal switch
    {
        AssetRefusal.NotFound => ServiceResult.NotFound($"{recordType}.AssetNotFound", ServiceErrorKeys.SelectedAssetNotFound),
        AssetRefusal.Deleted => ServiceResult.Conflict($"{recordType}.AssetDeleted", ServiceErrorKeys.AssetAlreadyDeleted),
        AssetRefusal.CurrencyMismatch => ServiceResult.Validation($"{recordType}.CurrencyMismatch", ServiceErrorKeys.CurrencyMismatch),
        _ => throw new ArgumentOutOfRangeException(nameof(refusal), refusal, "Not an AssetRefusal member."),
    };
}
