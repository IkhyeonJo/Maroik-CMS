using Maroik.Core.Domain.Finances;
namespace Maroik.Core.Domain.Tests.Finances;

/// <summary>
/// Unit tests for <see cref="Maroik.Core.Domain.Finances.Expenditure"/>.
/// Covers expenditure recording validation (empty main/subclass and payment method) and update logic.
/// </summary>
public class ExpenditureTests
{
    /// <summary>The fixed "current time" every domain call in this class receives.</summary>
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>A valid KRW lunch expenditure paid from "My Card".</summary>
    private static Expenditure ValidExpenditure() =>
        Expenditure.Record("user@example.com", "ConsumerSpending", "MealOrEatOutExpenses", "Lunch", 15000m, "KRW", "My Card", null, Now).Value;

    // -- Record ---------------------------------------------------------------

    /// <summary>Record returns expenditure, when valid.</summary>
    [Fact]
    public void Record_ReturnsExpenditure_WhenValid()
    {
        var result = Expenditure.Record("user@example.com", "ConsumerSpending", "MealOrEatOutExpenses", "Lunch", 15000m, "KRW", "My Card", null, Now);

        Assert.False(result.IsError);
        Assert.Equal("ConsumerSpending", result.Value.MainClass);
        Assert.Equal(15000m, result.Value.Amount.Amount);
    }

    /// <summary>Record returns error, when main class empty.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Record_ReturnsError_WhenMainClassEmpty(string? mainClass)
    {
        var result = Expenditure.Record("user@example.com", mainClass, "MealOrEatOutExpenses", null, 0m, "KRW", "My Card", null, Now);

        Assert.True(result.IsError);
        Assert.Equal("Expenditure.MainClassEmpty", result.FirstError.Code);
    }

    /// <summary>Record returns error, when sub class empty.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Record_ReturnsError_WhenSubClassEmpty(string? subClass)
    {
        var result = Expenditure.Record("user@example.com", "ConsumerSpending", subClass, null, 0m, "KRW", "My Card", null, Now);

        Assert.True(result.IsError);
        Assert.Equal("Expenditure.SubClassEmpty", result.FirstError.Code);
    }

    /// <summary>Record returns error, when payment method empty.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Record_ReturnsError_WhenPaymentMethodEmpty(string? method)
    {
        var result = Expenditure.Record("user@example.com", "ConsumerSpending", "MealOrEatOutExpenses", null, 0m, "KRW", method, null, Now);

        Assert.True(result.IsError);
        Assert.Equal("Expenditure.PaymentMethodEmpty", result.FirstError.Code);
    }

    /// <summary>Record returns error, when email invalid.</summary>
    [Fact]
    public void Record_ReturnsError_WhenEmailInvalid()
    {
        var result = Expenditure.Record("bademail", "ConsumerSpending", "MealOrEatOutExpenses", null, 0m, "KRW", "My Card", null, Now);

        Assert.True(result.IsError);
    }

    // -- Update ---------------------------------------------------------------

    /// <summary>Update succeeds, when valid.</summary>
    [Fact]
    public void Update_Succeeds_WhenValid()
    {
        var expenditure = ValidExpenditure();

        var result = expenditure.Update("NonConsumerSpending", "Tax", "Income tax", 300000m, "KRW", "Bank Account", null, "note", Now);

        Assert.False(result.IsError);
        Assert.Equal("NonConsumerSpending", expenditure.MainClass);
        Assert.Equal(300000m, expenditure.Amount.Amount);
    }

    /// <summary>Update returns error, when payment method empty.</summary>
    [Fact]
    public void Update_ReturnsError_WhenPaymentMethodEmpty()
    {
        var expenditure = ValidExpenditure();

        var result = expenditure.Update("ConsumerSpending", "MealOrEatOutExpenses", null, 0m, "KRW", "", null, null, Now);

        Assert.True(result.IsError);
        Assert.Equal("Expenditure.PaymentMethodEmpty", result.FirstError.Code);
    }

    // -- The transfer (deposit-asset) invariant ----------------------------------

    /// <summary>A transfer-type expenditure (savings, investment, pension, debt) moves money into a deposit asset, so it needs one.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void Record_ReturnsError_WhenATransferHasNoDepositAsset(string? depositAsset)
    {
        var result = Expenditure.Record("user@example.com", "RegularSavings", "Deposit", "Savings", 1000m, "KRW", "Checking", depositAsset, Now);

        Assert.Equal("Expenditure.DepositAssetEmpty", result.FirstError.Code);
    }

    /// <summary>A transfer cannot move money from an asset into itself.</summary>
    [Fact]
    public void Record_ReturnsError_WhenATransferNamesTheSameAssetOnBothSides()
    {
        var result = Expenditure.Record("user@example.com", "RegularSavings", "Deposit", "Savings", 1000m, "KRW", "Checking", "Checking", Now);

        Assert.Equal("Expenditure.SameAsset", result.FirstError.Code);
        Assert.Equal("The PaymentMethod and MyDepositAsset value cannot be the same.", result.FirstError.Description);
    }

    /// <summary>A transfer keeps its deposit asset; any other expenditure never has one, whatever was submitted.</summary>
    [Fact]
    public void Record_KeepsTheDepositAssetOfATransfer_AndDropsItForAnyOtherExpenditure()
    {
        var transfer = Expenditure.Record("user@example.com", "RegularSavings", "Deposit", "Savings", 1000m, "KRW", "Checking", "Savings", Now).Value;
        var lunch = Expenditure.Record("user@example.com", "ConsumerSpending", "MealOrEatOutExpenses", "Lunch", 1000m, "KRW", "Checking", "Savings", Now).Value;

        Assert.Equal("Savings", transfer.MyDepositAsset);
        Assert.Null(lunch.MyDepositAsset);
    }

    /// <summary>Update applies the same invariant: a transfer needs a different deposit asset, others drop theirs.</summary>
    [Fact]
    public void Update_EnforcesTheDepositAssetInvariant()
    {
        var expenditure = ValidExpenditure();

        Assert.Equal("Expenditure.DepositAssetEmpty",
            expenditure.Update("RegularSavings", "Investment", "Fund", 1m, "KRW", "Checking", null, null, Now).FirstError.Code);
        Assert.Equal("Expenditure.SameAsset",
            expenditure.Update("RegularSavings", "Investment", "Fund", 1m, "KRW", "Checking", "Checking", null, Now).FirstError.Code);

        Assert.False(expenditure.Update("ConsumerSpending", "PinMoney", "Gift", 1m, "KRW", "Checking", "Savings", null, Now).IsError);
        Assert.Null(expenditure.MyDepositAsset);
    }
}
