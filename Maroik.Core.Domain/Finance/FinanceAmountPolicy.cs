using ErrorOr;
using Maroik.Core.Domain.Errors;

namespace Maroik.Core.Domain.Finance;

/// <summary>
/// The <c>Amount</c> validation rule shared by every Finance aggregate (<see cref="Income"/>,
/// <see cref="Expenditure"/>, <see cref="FixedIncome"/>, <see cref="FixedExpenditure"/>).
/// Sibling to <see cref="FinanceTextPolicy"/>.
/// </summary>
public static class FinanceAmountPolicy
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
    /// The smallest amount step (one unit of the last stored decimal place), rendered as the amount
    /// inputs' <c>step</c> so the browser lets a user type every value the server accepts.
    /// </summary>
    public const decimal SmallestAmountStep = 0.0001m;

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
}
