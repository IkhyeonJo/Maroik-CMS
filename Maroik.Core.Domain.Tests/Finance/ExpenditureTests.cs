using Maroik.Core.Domain.Finance;
namespace Maroik.Core.Domain.Tests.Finance;

/// <summary>
/// Unit tests for <see cref="Maroik.Core.Domain.Finance.Expenditure"/>.
/// Covers expenditure recording validation (empty main/subclass and payment method) and update logic.
/// </summary>
public class ExpenditureTests
{
    private static Expenditure ValidExpenditure() =>
        Expenditure.Record("user@example.com", "ConsumerSpending", "MealOrEatOutExpenses", "Lunch", 15000m, "KRW", "My Card", null).Value;

    // -- Record ---------------------------------------------------------------

    /// <summary>Record returns expenditure, when valid.</summary>
    [Fact]
    public void Record_ReturnsExpenditure_WhenValid()
    {
        var result = Expenditure.Record("user@example.com", "ConsumerSpending", "MealOrEatOutExpenses", "Lunch", 15000m, "KRW", "My Card", null);

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
        var result = Expenditure.Record("user@example.com", mainClass, "MealOrEatOutExpenses", null, 0m, "KRW", "My Card", null);

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
        var result = Expenditure.Record("user@example.com", "ConsumerSpending", subClass, null, 0m, "KRW", "My Card", null);

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
        var result = Expenditure.Record("user@example.com", "ConsumerSpending", "MealOrEatOutExpenses", null, 0m, "KRW", method, null);

        Assert.True(result.IsError);
        Assert.Equal("Expenditure.PaymentMethodEmpty", result.FirstError.Code);
    }

    /// <summary>Record returns error, when email invalid.</summary>
    [Fact]
    public void Record_ReturnsError_WhenEmailInvalid()
    {
        var result = Expenditure.Record("bademail", "ConsumerSpending", "MealOrEatOutExpenses", null, 0m, "KRW", "My Card", null);

        Assert.True(result.IsError);
    }

    // -- Update ---------------------------------------------------------------

    /// <summary>Update succeeds, when valid.</summary>
    [Fact]
    public void Update_Succeeds_WhenValid()
    {
        var expenditure = ValidExpenditure();

        var result = expenditure.Update("NonConsumerSpending", "Tax", "Income tax", 300000m, "KRW", "Bank Account", null, "note");

        Assert.False(result.IsError);
        Assert.Equal("NonConsumerSpending", expenditure.MainClass);
        Assert.Equal(300000m, expenditure.Amount.Amount);
    }

    /// <summary>Update returns error, when payment method empty.</summary>
    [Fact]
    public void Update_ReturnsError_WhenPaymentMethodEmpty()
    {
        var expenditure = ValidExpenditure();

        var result = expenditure.Update("ConsumerSpending", "MealOrEatOutExpenses", null, 0m, "KRW", "", null, null);

        Assert.True(result.IsError);
        Assert.Equal("Expenditure.PaymentMethodEmpty", result.FirstError.Code);
    }
}
