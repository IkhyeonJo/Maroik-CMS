using Maroik.Core.Domain.Budgets;

namespace Maroik.Core.Domain.Tests.Budgets;

/// <summary>Unit tests for <see cref="BudgetSheetName"/>: the sheet names formulas use (Income2026, Expenditure!) and their parsing.</summary>
public class BudgetSheetNameTests
{
    /// <summary>A sheet's name is its kind and year.</summary>
    [Fact]
    public void Format_IsKindAndYear()
    {
        Assert.Equal("Income2026", BudgetSheetName.Format(BudgetKind.Income, 2026));
        Assert.Equal("Expenditure1", BudgetSheetName.Format(BudgetKind.Expenditure, 1));
    }

    /// <summary>A name with a year names that year; without one, the formula's own year; any case.</summary>
    [Theory]
    [InlineData("Income2025", BudgetKind.Income, 2025)]
    [InlineData("expenditure2026", BudgetKind.Expenditure, 2026)]
    [InlineData("INCOME", BudgetKind.Income, 2030)]
    [InlineData("Expenditure", BudgetKind.Expenditure, 2030)]
    [InlineData("Income9999", BudgetKind.Income, 9999)]
    public void TryParse_ReadsKindAndYear(string text, BudgetKind kind, int year)
    {
        Assert.True(BudgetSheetName.TryParse(text, 2030, out BudgetKind parsedKind, out int parsedYear));
        Assert.Equal((kind, year), (parsedKind, parsedYear));
    }

    /// <summary>Anything else is not a sheet: another word, a year outside 1..9999, a sign or leading zero.</summary>
    [Theory]
    [InlineData("Budget2026")]
    [InlineData("Income0")]
    [InlineData("Income10000")]
    [InlineData("Income-1")]
    [InlineData("Income02026")]
    [InlineData("Income 2026")]
    [InlineData("")]
    public void TryParse_RefusesOtherNames(string text) => Assert.False(BudgetSheetName.TryParse(text, 2030, out _, out _));
}
