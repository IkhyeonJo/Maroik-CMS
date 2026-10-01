using Maroik.Core.Domain.Finance;
using DomainLocalizableError = Maroik.Core.Domain.Localization.LocalizableError;
namespace Maroik.Core.Domain.Tests.Finance;

/// <summary>
/// Range / length limits that mirror the persisted columns (<c>numeric(20,4)</c> amounts,
/// <c>character varying(255)</c> names and notes): an over-limit value must be a clean domain
/// validation error, not a raw SQLSTATE 22003 / 22001 from the database write.
/// </summary>
public class FinanceAmountRangeTests
{
    /// <summary>The largest amount (in absolute value) the policy accepts.</summary>
    private const decimal Max = FinanceAmountPolicy.MaxAbsoluteAmount;
    /// <summary>The smallest four-decimal amount above <see cref="Max"/> (<see cref="Max"/> + 0.0001).</summary>
    private const decimal OverMax = 10_000_000_000_000_000m;

    // -- FinanceAmountPolicy ---------------------------------------------------

    /// <summary>The largest value <c>numeric(20,4)</c> holds is accepted, in either sign.</summary>
    [Theory]
    [InlineData("0")]
    [InlineData("1234.56")]
    [InlineData("9999999999999999.9999")]
    [InlineData("-9999999999999999.9999")]
    public void ValidateWithinRange_ReturnsSuccess_ForValuesTheColumnCanHold(string amount)
        => Assert.False(FinanceAmountPolicy.ValidateWithinRange(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)).IsError);

    /// <summary>Anything larger in magnitude is rejected.</summary>
    [Theory]
    [InlineData("10000000000000000")]
    [InlineData("-10000000000000000")]
    [InlineData("79228162514264337593543950335")]
    public void ValidateWithinRange_ReturnsError_WhenMagnitudeExceedsTheColumn(string amount)
    {
        var result = FinanceAmountPolicy.ValidateWithinRange(decimal.Parse(amount));

        Assert.True(result.IsError);
        Assert.Equal("Finance.AmountOutOfRange", result.FirstError.Code);
    }

    /// <summary>
    /// The limit quoted in the message is the real one, to the last stored decimal. (It used to be formatted without
    /// decimals and so rounded up to 10,000,000,000,000,000 — a value that is itself out of range.)
    /// </summary>
    [Fact]
    public void ValidateWithinRange_QuotesTheExactLimit()
    {
        var error = FinanceAmountPolicy.ValidateWithinRange(OverMax).FirstError;

        var args = (object[])error.Metadata![DomainLocalizableError.ResourceArgsMetadataKey];
        Assert.Equal("9,999,999,999,999,999.9999", args[0]);
        Assert.Contains("9,999,999,999,999,999.9999", error.Description);
    }

    /// <summary><c>ValidateAmount</c> applies both the non-negative and the range rule.</summary>
    [Fact]
    public void ValidateAmount_AppliesBothRules()
    {
        Assert.False(FinanceAmountPolicy.ValidateAmount(Max).IsError);
        Assert.Equal("Finance.NegativeAmount", FinanceAmountPolicy.ValidateAmount(-1m).FirstError.Code);
        Assert.Equal("Finance.AmountOutOfRange", FinanceAmountPolicy.ValidateAmount(OverMax).FirstError.Code);
    }

    // -- every amount-carrying aggregate ---------------------------------------

    /// <summary>Income.Record rejects an over-range amount.</summary>
    [Fact]
    public void Income_Record_ReturnsError_WhenAmountOutOfRange()
    {
        var result = Income.Record("user@example.com", "RegularIncome", "LaborIncome", "salary", OverMax, "KRW", "Wallet");

        Assert.Equal("Finance.AmountOutOfRange", result.FirstError.Code);
    }

    /// <summary>Income.Record accepts the largest storable amount.</summary>
    [Fact]
    public void Income_Record_Succeeds_AtTheMaximumAmount()
        => Assert.False(Income.Record("user@example.com", "RegularIncome", "LaborIncome", "salary", Max, "KRW", "Wallet").IsError);

    /// <summary>Expenditure.Record rejects an over-range amount.</summary>
    [Fact]
    public void Expenditure_Record_ReturnsError_WhenAmountOutOfRange()
    {
        var result = Expenditure.Record("user@example.com", "ConsumerSpending", "MealOrEatOutExpenses", "lunch", OverMax, "KRW", "Wallet", null);

        Assert.Equal("Finance.AmountOutOfRange", result.FirstError.Code);
    }

    /// <summary>FixedIncome.Register rejects an over-range amount.</summary>
    [Fact]
    public void FixedIncome_Register_ReturnsError_WhenAmountOutOfRange()
    {
        var result = FixedIncome.Register("user@example.com", "RegularIncome", "LaborIncome", "salary", OverMax, "KRW",
            "Wallet", 1, 15, new DateTime(2099, 12, 31));

        Assert.Equal("Finance.AmountOutOfRange", result.FirstError.Code);
    }

    /// <summary>FixedExpenditure.Register rejects an over-range amount.</summary>
    [Fact]
    public void FixedExpenditure_Register_ReturnsError_WhenAmountOutOfRange()
    {
        var result = FixedExpenditure.Register("user@example.com", "ConsumerSpending", "MealOrEatOutExpenses", "rent", OverMax, "KRW",
            "Wallet", null, 1, 15, new DateTime(2099, 12, 31));

        Assert.Equal("Finance.AmountOutOfRange", result.FirstError.Code);
    }

    // -- Asset -------------------------------------------------------------------

    /// <summary>An asset whose name / note / balance sit exactly on the column limits is valid.</summary>
    [Fact]
    public void Asset_Create_Succeeds_AtTheColumnLimits()
    {
        var result = Asset.Create(new string('a', 255), "user@example.com", "FreeDepositAndWithdrawal", Max, "KRW", new string('n', 255));

        Assert.False(result.IsError);
    }

    /// <summary>A negative balance is still allowed (overdraft / debt), down to the column's limit.</summary>
    [Fact]
    public void Asset_Create_Succeeds_WithANegativeBalance()
        => Assert.False(Asset.Create("Loan", "user@example.com", "FreeDepositAndWithdrawal", -Max, "KRW").IsError);

    /// <summary>A product name over 255 characters is rejected.</summary>
    [Fact]
    public void Asset_Create_ReturnsError_WhenProductNameTooLong()
    {
        var result = Asset.Create(new string('a', 256), "user@example.com", "FreeDepositAndWithdrawal", 1m, "KRW");

        Assert.Equal("Asset.ProductNameTooLong", result.FirstError.Code);
    }

    /// <summary>A note over 255 characters is rejected.</summary>
    [Fact]
    public void Asset_Create_ReturnsError_WhenNoteTooLong()
    {
        var result = Asset.Create("Wallet", "user@example.com", "FreeDepositAndWithdrawal", 1m, "KRW", new string('n', 256));

        Assert.Equal("Finance.NoteTooLong", result.FirstError.Code);
    }

    /// <summary>A balance beyond the column's range is rejected.</summary>
    [Theory]
    [InlineData("10000000000000000")]
    [InlineData("-10000000000000000")]
    public void Asset_Create_ReturnsError_WhenBalanceOutOfRange(string amount)
    {
        var result = Asset.Create("Wallet", "user@example.com", "FreeDepositAndWithdrawal", decimal.Parse(amount), "KRW");

        Assert.Equal("Finance.AmountOutOfRange", result.FirstError.Code);
    }

    /// <summary>Asset.Update applies the same limits as Create.</summary>
    [Fact]
    public void Asset_Update_AppliesTheSameLimits()
    {
        var asset = Asset.Create("Wallet", "user@example.com", "FreeDepositAndWithdrawal", 1m, "KRW").Value;

        Assert.Equal("Asset.ProductNameTooLong", asset.Update(new string('a', 256), "FreeDepositAndWithdrawal", 1m, "KRW", null, false).FirstError.Code);
        Assert.Equal("Finance.NoteTooLong", asset.Update("Wallet", "FreeDepositAndWithdrawal", 1m, "KRW", new string('n', 256), false).FirstError.Code);
        Assert.Equal("Finance.AmountOutOfRange", asset.Update("Wallet", "FreeDepositAndWithdrawal", OverMax, "KRW", null, false).FirstError.Code);
        Assert.False(asset.Update(new string('a', 255), "FreeDepositAndWithdrawal", Max, "KRW", new string('n', 255), false).IsError);
    }
}
