using Maroik.Core.Domain.Finance;
namespace Maroik.Core.Domain.Tests.Finance;

/// <summary>
/// Unit tests for <see cref="Maroik.Core.Domain.Finance.Income"/>.
/// Covers income recording validation (empty main/subclass, deposit asset) and update logic.
/// </summary>
public class IncomeTests
{
    /// <summary>The fixed "current time" every domain call in this class receives.</summary>
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>A valid KRW salary income deposited into "My Bank".</summary>
    private static Income ValidIncome() =>
        Income.Record("user@example.com", "RegularIncome", "LaborIncome", "Salary", 3000000m, "KRW", "My Bank", Now).Value;

    // -- Record ---------------------------------------------------------------

    /// <summary>Record returns income, when valid.</summary>
    [Fact]
    public void Record_ReturnsIncome_WhenValid()
    {
        var result = Income.Record("user@example.com", "RegularIncome", "LaborIncome", "Salary", 3000000m, "KRW", "My Bank", Now);

        Assert.False(result.IsError);
        Assert.Equal("RegularIncome", result.Value.MainClass);
        Assert.Equal(3000000m, result.Value.Amount.Amount);
    }

    /// <summary>Record returns error, when main class empty.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Record_ReturnsError_WhenMainClassEmpty(string? mainClass)
    {
        var result = Income.Record("user@example.com", mainClass, "LaborIncome", null, 0m, "KRW", "My Bank", Now);

        Assert.True(result.IsError);
        Assert.Equal("Income.MainClassEmpty", result.FirstError.Code);
    }

    /// <summary>Record returns error, when subclass empty.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Record_ReturnsError_WhenSubClassEmpty(string? subClass)
    {
        var result = Income.Record("user@example.com", "RegularIncome", subClass, null, 0m, "KRW", "My Bank", Now);

        Assert.True(result.IsError);
        Assert.Equal("Income.SubClassEmpty", result.FirstError.Code);
    }

    /// <summary>Record returns error, when deposit asset empty.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Record_ReturnsError_WhenDepositAssetEmpty(string? assetName)
    {
        var result = Income.Record("user@example.com", "RegularIncome", "LaborIncome", null, 0m, "KRW", assetName, Now);

        Assert.True(result.IsError);
        Assert.Equal("Income.DepositAssetEmpty", result.FirstError.Code);
    }

    /// <summary>Record returns error, when email invalid.</summary>
    [Fact]
    public void Record_ReturnsError_WhenEmailInvalid()
    {
        var result = Income.Record("bademail", "RegularIncome", "LaborIncome", null, 0m, "KRW", "My Bank", Now);

        Assert.True(result.IsError);
    }

    // -- Update ---------------------------------------------------------------

    /// <summary>Update succeeds, when valid.</summary>
    [Fact]
    public void Update_Succeeds_WhenValid()
    {
        var income = ValidIncome();

        var result = income.Update("RegularIncome", "FinancialIncome", "Dividend", 500000m, "KRW", "Stock Account", "note", Now);

        Assert.False(result.IsError);
        Assert.Equal("RegularIncome", income.MainClass);
        Assert.Equal(500000m, income.Amount.Amount);
        Assert.Equal("note", income.Note);
    }

    /// <summary>Update returns error, when main class empty.</summary>
    [Fact]
    public void Update_ReturnsError_WhenMainClassEmpty()
    {
        var income = ValidIncome();

        var result = income.Update("", "LaborIncome", null, 0m, "KRW", "My Bank", null, Now);

        Assert.True(result.IsError);
        Assert.Equal("Income.MainClassEmpty", result.FirstError.Code);
    }
}
