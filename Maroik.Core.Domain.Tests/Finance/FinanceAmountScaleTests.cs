using System.Globalization;
using Maroik.Core.Domain.Finance;
using Maroik.Core.Domain.ValueObjects;
using DomainLocalizableError = Maroik.Core.Domain.Localization.LocalizableError;
namespace Maroik.Core.Domain.Tests.Finance;

/// <summary>
/// Amounts are stored in <c>numeric(20,4)</c> columns. A typed amount with more than four decimal
/// places is a validation error — never rounded silently, because the database would round the
/// transaction and the asset balance separately and let them drift apart. The rule is the same for
/// every currency.
/// </summary>
public class FinanceAmountScaleTests
{
    /// <summary>The fixed "current time" every domain call in this class receives.</summary>
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>An amount with five decimal places, one more than the columns store.</summary>
    private const decimal FiveDecimals = 0.00005m;

    /// <summary>Asserts <paramref name="code"/> is the "too many decimals" validation error.</summary>
    private static void AssertTooManyDecimals(string code) => Assert.Equal("Finance.AmountTooManyDecimals", code);

    /// <summary>Up to four decimal places are accepted, whatever trailing zeros the value carries.</summary>
    [Theory]
    [InlineData("0")]
    [InlineData("0.005")]
    [InlineData("1000.5")]
    [InlineData("0.0001")]
    [InlineData("-12.3456")]
    [InlineData("1.50000000")]
    [InlineData("9999999999999999.9999")]
    public void ValidateScale_Accepts_UpToFourDecimalPlaces(string amount)
        => Assert.False(FinanceAmountPolicy.ValidateScale(decimal.Parse(amount, CultureInfo.InvariantCulture)).IsError);

    /// <summary>The input step the views render is one unit of the last stored decimal place, and is itself accepted.</summary>
    [Fact]
    public void SmallestAmountStep_IsOneUnitOfTheLastStoredDecimalPlace()
    {
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(FinanceAmountPolicy).TypeHandle);

        Assert.Equal(FinanceAmountPolicy.SmallestAmountStep, 1m / (decimal)Math.Pow(10, FinanceAmountPolicy.MaxDecimalPlaces));
        Assert.False(FinanceAmountPolicy.ValidateScale(FinanceAmountPolicy.SmallestAmountStep).IsError);
        Assert.True(FinanceAmountPolicy.ValidateScale(FinanceAmountPolicy.SmallestAmountStep / 10).IsError);
    }

    /// <summary>A fifth significant decimal place is rejected, in either sign.</summary>
    [Theory]
    [InlineData("0.00001")]
    [InlineData("0.00005")]
    [InlineData("100.12345")]
    [InlineData("-0.00001")]
    public void ValidateScale_Rejects_MoreThanFourDecimalPlaces(string amount)
    {
        var error = FinanceAmountPolicy.ValidateScale(decimal.Parse(amount, CultureInfo.InvariantCulture)).FirstError;

        AssertTooManyDecimals(error.Code);
        Assert.Equal("Amount can have up to 4 decimal places.", error.Description);
        Assert.Equal("Amount can have up to {0} decimal places.", error.Metadata![DomainLocalizableError.ResourceKeyMetadataKey]);
    }

    /// <summary><c>ValidateAmount</c> (every income / expenditure) includes the scale rule.</summary>
    [Fact]
    public void ValidateAmount_AppliesTheScaleRule()
        => AssertTooManyDecimals(FinanceAmountPolicy.ValidateAmount(FiveDecimals).FirstError.Code);

    /// <summary>Income: record and update.</summary>
    [Fact]
    public void Income_RecordAndUpdate_RejectFiveDecimals()
    {
        AssertTooManyDecimals(Income.Record("user@example.com", "RegularIncome", "LaborIncome", "salary", FiveDecimals, "KRW", "Wallet", Now).FirstError.Code);

        var income = Income.Record("user@example.com", "RegularIncome", "LaborIncome", "salary", 1000.5m, "KRW", "Wallet", Now).Value;
        Assert.Equal(1000.5m, income.Amount.Amount);
        AssertTooManyDecimals(income.Update("RegularIncome", "LaborIncome", "salary", FiveDecimals, "KRW", "Wallet", null, Now).FirstError.Code);
    }

    /// <summary>Expenditure: record and update.</summary>
    [Fact]
    public void Expenditure_RecordAndUpdate_RejectFiveDecimals()
    {
        AssertTooManyDecimals(Expenditure.Record("user@example.com", "ConsumerSpending", "MealOrEatOutExpenses", "lunch", FiveDecimals, "KRW", "Wallet", null, Now).FirstError.Code);

        var expenditure = Expenditure.Record("user@example.com", "ConsumerSpending", "MealOrEatOutExpenses", "lunch", 0.005m, "KRW", "Wallet", null, Now).Value;
        Assert.Equal(0.005m, expenditure.Amount.Amount);
        AssertTooManyDecimals(expenditure.Update("ConsumerSpending", "MealOrEatOutExpenses", "lunch", FiveDecimals, "KRW", "Wallet", null, null, Now).FirstError.Code);
    }

    /// <summary>FixedIncome: register and update.</summary>
    [Fact]
    public void FixedIncome_RegisterAndUpdate_RejectFiveDecimals()
    {
        AssertTooManyDecimals(FixedIncome.Register("user@example.com", "RegularIncome", "LaborIncome", "salary", FiveDecimals, "KRW",
            "Wallet", 1, 15, new DateTime(2099, 12, 31), Now).FirstError.Code);

        var fixedIncome = FixedIncome.Register("user@example.com", "RegularIncome", "LaborIncome", "salary", 0.0001m, "KRW",
            "Wallet", 1, 15, new DateTime(2099, 12, 31), Now).Value;
        AssertTooManyDecimals(fixedIncome.Update("RegularIncome", "LaborIncome", "salary", FiveDecimals, "KRW",
            "Wallet", 1, 15, new DateTime(2099, 12, 31), null, Now).FirstError.Code);
    }

    /// <summary>FixedExpenditure: register and update.</summary>
    [Fact]
    public void FixedExpenditure_RegisterAndUpdate_RejectFiveDecimals()
    {
        AssertTooManyDecimals(FixedExpenditure.Register("user@example.com", "ConsumerSpending", "MealOrEatOutExpenses", "rent", FiveDecimals, "KRW",
            "Wallet", null, 1, 15, new DateTime(2099, 12, 31), Now).FirstError.Code);

        var fixedExpenditure = FixedExpenditure.Register("user@example.com", "ConsumerSpending", "MealOrEatOutExpenses", "rent", 0.0001m, "KRW",
            "Wallet", null, 1, 15, new DateTime(2099, 12, 31), Now).Value;
        AssertTooManyDecimals(fixedExpenditure.Update("ConsumerSpending", "MealOrEatOutExpenses", "rent", FiveDecimals, "KRW",
            "Wallet", null, 1, 15, new DateTime(2099, 12, 31), null, Now).FirstError.Code);
    }

    /// <summary>Asset: a typed balance on create, update and a direct set.</summary>
    [Fact]
    public void Asset_CreateUpdateAndSetBalance_RejectFiveDecimals()
    {
        AssertTooManyDecimals(Asset.Create("Wallet", "user@example.com", "FreeDepositAndWithdrawal", FiveDecimals, "KRW", Now).FirstError.Code);

        var asset = Asset.Create("Wallet", "user@example.com", "FreeDepositAndWithdrawal", 100m, "KRW", Now).Value;
        AssertTooManyDecimals(asset.Update("Wallet", "FreeDepositAndWithdrawal", FiveDecimals, "KRW", null, false, Now).FirstError.Code);
        AssertTooManyDecimals(asset.SetBalance(Money.Create(FiveDecimals, "KRW").Value, Now).FirstError.Code);
        Assert.Equal(100m, asset.Balance.Amount);
    }

    /// <summary>A currency label change is never refused by the scale rule (or anything else).</summary>
    [Fact]
    public void Asset_Update_StillAcceptsACurrencyChange()
    {
        var asset = Asset.Create("Wallet", "user@example.com", "FreeDepositAndWithdrawal", 1000.5m, "원", Now).Value;

        Assert.False(asset.Update("Wallet", "FreeDepositAndWithdrawal", 1000.5m, "KRW", null, false, Now).IsError);
        Assert.Equal("KRW", asset.Balance.Currency);
    }

    /// <summary>
    /// The drift scenario: withdrawing then re-depositing 0.005 from a 100.00 balance restores it
    /// exactly (four decimals are kept, nothing is rounded).
    /// </summary>
    [Fact]
    public void Asset_WithdrawThenDeposit_OfAFractionalAmount_RestoresTheBalanceExactly()
    {
        var asset = Asset.Create("Wallet", "user@example.com", "FreeDepositAndWithdrawal", 100.00m, "KRW", Now).Value;
        var spent = Money.Create(0.005m, "KRW").Value;

        Assert.False(asset.Withdraw(spent, Now).IsError);
        Assert.Equal(99.995m, asset.Balance.Amount);
        Assert.False(asset.Deposit(spent, Now).IsError);
        Assert.Equal(100m, asset.Balance.Amount);
    }
}
