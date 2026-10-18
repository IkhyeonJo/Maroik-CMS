using System.Globalization;
using System.Text.RegularExpressions;
using ErrorOr;
using Maroik.Core.Domain.Errors;

namespace Maroik.Core.Domain.Finances;

/// <summary>
/// The <c>Amount</c> validation rule shared by every Finance aggregate (<see cref="Income"/>,
/// <see cref="Expenditure"/>, <see cref="FixedIncome"/>, <see cref="FixedExpenditure"/>).
/// Sibling to <see cref="FinanceTextPolicy"/>.
/// </summary>
public static partial class FinanceAmountPolicy
{
    /// <summary>
    /// Returns a validation error when <paramref name="amount"/> is negative; otherwise
    /// <see cref="Result.Success"/>.
    /// </summary>
    public static ErrorOr<Success> ValidateNonNegative(decimal amount)
    {
        if (amount < 0m)
            return DomainError.Validation("Finance.NegativeAmount", "Amount cannot be negative.");

        return Result.Success;
    }

    /// <summary>
    /// Largest absolute amount the persisted <c>numeric(20,4)</c> columns (Asset / Income / Expenditure /
    /// FixedIncome / FixedExpenditure <c>Amount</c>) can hold: 16 integer digits and 4 decimals.
    /// </summary>
    public const decimal MaxAbsoluteAmount = 9_999_999_999_999_999.9999m;

    /// <summary>
    /// Rejects an amount whose magnitude the database column cannot hold, so an over-large value is a
    /// clean validation error instead of a raw "numeric field overflow" (SQLSTATE 22003) from the write.
    /// </summary>
    public static ErrorOr<Success> ValidateWithinRange(decimal amount)
    {
        if (decimal.Abs(amount) > MaxAbsoluteAmount)
            return DomainError.Validation("Finance.AmountOutOfRange", "Amount must not exceed {0}.", MaxAbsoluteAmount.ToString("N4", System.Globalization.CultureInfo.InvariantCulture));

        return Result.Success;
    }

    /// <summary>
    /// Decimal places the persisted <c>numeric(20,4)</c> amount columns store. The same for every
    /// currency: there is no per-currency rule (1,000.5 KRW is stored as typed).
    /// </summary>
    public const int MaxDecimalPlaces = 4;

    /// <summary>
    /// Rejects an amount with more than <see cref="MaxDecimalPlaces"/> significant decimal places
    /// (trailing zeros do not count). The amount is never rounded silently: the database would round
    /// a transaction and the asset balance it moves separately, so the two would drift apart.
    /// </summary>
    public static ErrorOr<Success> ValidateScale(decimal amount)
    {
        if (decimal.Round(amount, MaxDecimalPlaces) != amount)
            return DomainError.Validation("Finance.AmountTooManyDecimals", "Amount can have up to {0} decimal places.", MaxDecimalPlaces);

        return Result.Success;
    }

    /// <summary>
    /// Within the persisted column's range and its decimal places — the check a typed asset balance
    /// (which may be negative) goes through.
    /// </summary>
    public static ErrorOr<Success> ValidateBalance(decimal amount)
    {
        var range = ValidateWithinRange(amount);
        return range.IsError ? range : ValidateScale(amount);
    }

    /// <summary>
    /// Non-negative, within the persisted column's range and its decimal places — the check every
    /// income / expenditure (one-time or fixed) applies to its amount.
    /// </summary>
    public static ErrorOr<Success> ValidateAmount(decimal amount)
    {
        var nonNegative = ValidateNonNegative(amount);
        return nonNegative.IsError ? nonNegative : ValidateBalance(amount);
    }

    /// <summary>
    /// Most significant digits a <see cref="decimal"/> always holds exactly. Text with more is refused by
    /// <see cref="TryParseInput"/> rather than rounded.
    /// </summary>
    private const int MaxExactSignificantDigits = 28;

    /// <summary>
    /// The text an amount field posts: an optional leading minus, the integer digits either plain
    /// ("1234567") or with a comma between every group of three ("1,234,567"), and an optional decimal
    /// part. A trailing decimal point with no digits after it ("1.") is accepted: it is what the field
    /// holds while the user is about to type the decimals. ASCII digits only — <c>\d</c> would also
    /// match other scripts' digits, which <see cref="decimal.Parse(string, NumberStyles, IFormatProvider)"/> refuses.
    /// </summary>
    [GeneratedRegex(@"^(?<sign>-)?(?<integer>[0-9]{1,3}(?:,[0-9]{3})+|[0-9]+)(?:\.(?<fraction>[0-9]*))?\z")]
    private static partial Regex AmountInputRegex();

    /// <summary>
    /// Parses the amount text a finance form (account book or fixed income / expenditure) posts (<c>"1,234,567.5"</c>, see
    /// <see cref="AmountInputRegex"/>). Returns <see langword="false"/> (and 0) for text of any other
    /// shape, and for text with more than <see cref="MaxExactSignificantDigits"/> significant digits,
    /// which a <see cref="decimal"/> could only hold rounded. The range and the number of decimal places
    /// are left to <see cref="ValidateBalance"/> / <see cref="ValidateAmount"/>, which report them with
    /// their own messages.
    /// </summary>
    public static bool TryParseInput(string? input, out decimal amount)
    {
        amount = 0m;
        if (input is null)
            return false;

        var match = AmountInputRegex().Match(input);
        if (!match.Success)
            return false;

        string integer = match.Groups["integer"].Value.Replace(",", "", StringComparison.Ordinal);
        string fraction = match.Groups["fraction"].Value.TrimEnd('0');
        if ((integer.TrimStart('0') + fraction).Length > MaxExactSignificantDigits)
            return false;

        // At most 28 significant digits: always within decimal's range and held exactly.
        amount = decimal.Parse(match.Groups["sign"].Value + integer + (fraction.Length > 0 ? "." + fraction : ""),
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        return true;
    }
}
