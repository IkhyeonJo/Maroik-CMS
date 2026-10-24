using Maroik.Core.Domain.Budgets;
using Maroik.Core.Domain.Tests.Fixtures;

namespace Maroik.Core.Domain.Tests.Budgets;

/// <summary>Unit tests for <see cref="BudgetLine"/>: a line of a sheet (main class, sub-class, currency) and its stable row key.</summary>
public class BudgetLineTests
{
    /// <summary>A valid line keeps its classes and the normalized currency.</summary>
    [Fact]
    public void Create_AcceptsAClassOfTheKind()
    {
        BudgetLine line = BudgetLine.Create(BudgetKind.Income, "IrregularIncome", "LaborIncome", " usd ").Value;

        Assert.Equal(("IrregularIncome", "LaborIncome", "USD"), (line.MainClass, line.SubClass, line.Currency));
    }

    /// <summary>A class outside the kind's taxonomy is refused with the class policy's error; a bad currency with the currency's.</summary>
    [Fact]
    public void Create_RefusesWhatThePoliciesRefuse()
    {
        ErrorAssert.Validation(BudgetLine.Create(BudgetKind.Income, "ConsumerSpending", "PinMoney", "KRW"), "IncomeClass.Invalid", "Unknown income main class.");
        ErrorAssert.Validation(BudgetLine.Create(BudgetKind.Expenditure, "ConsumerSpending", "Deposit", "KRW"), "ExpenditureClass.Invalid", "Invalid sub-class for {0}.", "ConsumerSpending");
        ErrorAssert.Validation(BudgetLine.Create(BudgetKind.Income, "RegularIncome", "LaborIncome", ""), "Money.CurrencyEmpty", "Currency code cannot be empty.");
        ErrorAssert.Validation(BudgetLine.Create((BudgetKind)9, "RegularIncome", "LaborIncome", "KRW"), "Budget.KindInvalid", "Unknown budget kind.");
    }

    /// <summary>The key is stable across years and sheets (classes and the currency's UTF-8 bytes in hex), so formulas survive row changes.</summary>
    [Fact]
    public void TheKey_IsMadeOfTheClassesAndCurrency()
    {
        Assert.Equal("L.ConsumerSpending.PinMoney.4B5257", BudgetLine.Create(BudgetKind.Expenditure, "ConsumerSpending", "PinMoney", "krw").Value.Key);
        Assert.Equal("L.RegularIncome.LaborIncome.EC9B90", BudgetLine.Create(BudgetKind.Income, "RegularIncome", "LaborIncome", "원").Value.Key);
    }

    /// <summary>A sheet's default lines are every class of its kind in one currency, in taxonomy order; none without a currency.</summary>
    [Fact]
    public void Defaults_AreEveryClassInOneCurrency()
    {
        IReadOnlyList<BudgetLine> lines = BudgetLine.Defaults(BudgetKind.Income, "KRW");

        Assert.Equal(8, lines.Count);
        Assert.Equal(("RegularIncome", "LaborIncome"), (lines[0].MainClass, lines[0].SubClass));
        Assert.Equal(("IrregularIncome", "OtherIncome"), (lines[7].MainClass, lines[7].SubClass));
        Assert.All(lines, l => Assert.Equal("KRW", l.Currency));
        Assert.Empty(BudgetLine.Defaults(BudgetKind.Expenditure, null));
        Assert.Equal(20, BudgetLine.Defaults(BudgetKind.Expenditure, "KRW").Count);
    }
}
