using Maroik.Core.Domain.Budgets;
using Maroik.Core.Domain.Finances;

namespace Maroik.Core.Domain.Tests.Budgets;

/// <summary>Unit tests for <see cref="BudgetActuals"/>: real income / expenditure records summed per class, currency and local month.</summary>
public class BudgetActualsTests
{
    /// <summary>An income record.</summary>
    private static Income In(string main, string sub, decimal amount, string currency, DateTime createdUtc) =>
        Income.Reconstitute(1, "user@example.com", main, sub, "c", amount, currency, "asset", null, createdUtc, createdUtc);

    /// <summary>An expenditure record.</summary>
    private static Expenditure Out(string main, string sub, decimal amount, string currency, DateTime createdUtc) =>
        Expenditure.Reconstitute(1, "user@example.com", main, sub, "c", amount, currency, "card", null, null, createdUtc, createdUtc);

    /// <summary>Records are summed per kind, class, currency and the month they fall in, in the owner's time zone.</summary>
    [Fact]
    public void Records_AreSummedPerLocalMonth()
    {
        BudgetActuals actuals = BudgetActuals.From(
            [In("RegularIncome", "LaborIncome", 100m, "krw", new DateTime(2026, 5, 31, 16, 0, 0)), In("RegularIncome", "LaborIncome", 50m, "KRW", new DateTime(2026, 6, 2))],
            [Out("ConsumerSpending", "PinMoney", 7m, "USD", new DateTime(2026, 5, 3)), Out("ConsumerSpending", "PinMoney", 3m, "USD", new DateTime(2026, 5, 4))],
            "Asia/Seoul");

        BudgetLine labor = BudgetLine.Create(BudgetKind.Income, "RegularIncome", "LaborIncome", "KRW").Value;
        BudgetLine pin = BudgetLine.Create(BudgetKind.Expenditure, "ConsumerSpending", "PinMoney", "USD").Value;
        Assert.Equal(150m, actuals.Of(BudgetKind.Income, 2026, 6, labor));
        Assert.Equal(0m, actuals.Of(BudgetKind.Income, 2026, 5, labor));
        Assert.Equal(10m, actuals.Of(BudgetKind.Expenditure, 2026, 5, pin));
        Assert.Equal(0m, actuals.Of(BudgetKind.Income, 2026, 5, pin));
    }

    /// <summary>The lines a year has records for, each with its twelve monthly totals.</summary>
    [Fact]
    public void LinesOf_ListsTheYearsRecordedLines()
    {
        BudgetActuals actuals = BudgetActuals.From([], [Out("ConsumerSpending", "PinMoney", 7m, "KRW", new DateTime(2026, 3, 3)), Out("ConsumerSpending", "PinMoney", 1m, "KRW", new DateTime(2025, 3, 3))], "UTC");

        var (line, months) = Assert.Single(actuals.LinesOf(BudgetKind.Expenditure, 2026));
        Assert.Equal("PinMoney", line.SubClass);
        Assert.Equal(7m, months[2]);
        Assert.Equal(12, months.Count);
        Assert.Empty(actuals.LinesOf(BudgetKind.Income, 2026));
    }
}
