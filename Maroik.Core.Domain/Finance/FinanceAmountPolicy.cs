using ErrorOr;
using Maroik.Core.Domain.Localization;

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
            return LocalizableError.Validation("Finance.NegativeAmount", "Amount cannot be negative.");

        return Result.Success;
    }

    /// <summary>
    /// Largest absolute amount the persisted <c>numeric(18,2)</c> columns (Asset / Income / Expenditure /
    /// FixedIncome / FixedExpenditure <c>Amount</c>) can hold: 16 integer digits and 2 decimals.
    /// </summary>
    public const decimal MaxAbsoluteAmount = 9_999_999_999_999_999.99m;

    /// <summary>
    /// Rejects an amount whose magnitude the database column cannot hold, so an over-large value is a
    /// clean validation error instead of a raw "numeric field overflow" (SQLSTATE 22003) from the write.
    /// </summary>
    public static ErrorOr<Success> ValidateWithinRange(decimal amount)
    {
        if (decimal.Abs(amount) > MaxAbsoluteAmount)
            return LocalizableError.Validation("Finance.AmountOutOfRange", "Amount must not exceed {0}.", MaxAbsoluteAmount.ToString("N2", System.Globalization.CultureInfo.InvariantCulture));

        return Result.Success;
    }

    /// <summary>
    /// Non-negative <em>and</em> within the persisted column's range — the check every income /
    /// expenditure (one-time or fixed) applies to its amount.
    /// </summary>
    public static ErrorOr<Success> ValidateAmount(decimal amount)
    {
        var nonNegative = ValidateNonNegative(amount);
        return nonNegative.IsError ? nonNegative : ValidateWithinRange(amount);
    }
}
