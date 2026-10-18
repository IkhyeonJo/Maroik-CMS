namespace Maroik.Core.Domain.Finances;

/// <summary>
/// Why <see cref="AssetUsagePolicy"/> refuses to let an income / expenditure record (one-off or fixed) use the
/// assets it names. The policy only decides; each service answers a refusal with its own result (code, message key,
/// error type), so the wording and the resx keys stay where the controllers look them up.
/// </summary>
public enum AssetRefusal
{
    /// <summary>A required asset does not exist on the caller's account.</summary>
    NotFound,

    /// <summary>An asset the record would use or move has been soft-deleted; a deleted asset's balance is never touched.</summary>
    Deleted,

    /// <summary>A transfer's payment asset and deposit asset carry different currency labels.</summary>
    CurrencyMismatch,
}
