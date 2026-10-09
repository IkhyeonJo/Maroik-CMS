using ErrorOr;
using Maroik.Core.Domain.Errors;

namespace Maroik.Core.Domain.Finance;

/// <summary>
/// Length limits for the free-text fields (<c>Content</c>, <c>Note</c>) shared by every Finance
/// aggregate (<see cref="Income"/>, <see cref="Expenditure"/>, <see cref="FixedIncome"/>,
/// <see cref="FixedExpenditure"/>). The persisted columns are <c>character varying(255)</c>, so a
/// longer value would otherwise surface as a raw database exception ("Input is invalid") instead of
/// a clean domain validation error with its own message. Authoritative here; the create/edit forms mirror it.
/// </summary>
public static class FinanceTextPolicy
{
    /// <summary>Maximum length, in characters, of a Finance <c>Content</c> or <c>Note</c> value.</summary>
    public const int MaxTextLength = 255;

    /// <summary>
    /// Returns a validation error when <paramref name="content"/> or <paramref name="note"/> exceeds
    /// <see cref="MaxTextLength"/>; otherwise <see cref="Result.Success"/>. A null value is allowed
    /// (the field is optional).
    /// </summary>
    public static ErrorOr<Success> ValidateContentAndNote(string? content, string? note)
    {
        if (content is { Length: > MaxTextLength })
            return DomainError.Validation("Finance.ContentTooLong", "Content must be {0} characters or fewer.", MaxTextLength);

        if (note is { Length: > MaxTextLength })
            return DomainError.Validation("Finance.NoteTooLong", "Note must be {0} characters or fewer.", MaxTextLength);

        return Result.Success;
    }
}
