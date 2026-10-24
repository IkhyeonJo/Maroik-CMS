using Maroik.Core.Domain.Budgets;

namespace Maroik.Core.Domain.Tests.Budgets;

/// <summary>
/// Unit tests for <see cref="BudgetSheetLayout"/>: the fixed rows (main-class headers, lines, subtotals, totals) and free rows of a
/// sheet, what each cell is (label, budget, computed, blank, free) and the formulas of the computed cells.
/// </summary>
public class BudgetSheetLayoutTests
{
    /// <summary>A line.</summary>
    private static BudgetLine L(string main, string sub, string currency = "KRW", BudgetKind kind = BudgetKind.Expenditure) =>
        BudgetLine.Create(kind, main, sub, currency).Value;

    /// <summary>A layout of expenditure lines.</summary>
    private static BudgetSheetLayout Layout(params BudgetLine[] lines) => BudgetSheetLayout.Create(BudgetKind.Expenditure, lines);

    /// <summary>
    /// Rows: per main class (taxonomy order) a header, its lines (sub-class order, then currency), a subtotal per currency; then a
    /// total per currency; then the free rows.
    /// </summary>
    [Fact]
    public void Rows_FollowTheTaxonomy()
    {
        BudgetSheetLayout layout = Layout(L("ConsumerSpending", "PinMoney"), L("RegularSavings", "Deposit", "USD"), L("ConsumerSpending", "MealOrEatOutExpenses", "USD"),
            L("ConsumerSpending", "MealOrEatOutExpenses"));

        Assert.Equal([
            "H.RegularSavings", "L.RegularSavings.Deposit.555344", "S.RegularSavings.555344",
            "H.ConsumerSpending", "L.ConsumerSpending.MealOrEatOutExpenses.4B5257", "L.ConsumerSpending.MealOrEatOutExpenses.555344",
            "L.ConsumerSpending.PinMoney.4B5257", "S.ConsumerSpending.4B5257", "S.ConsumerSpending.555344",
            "T.4B5257", "T.555344",
        ], layout.Rows.Take(11).Select(r => r.Key));
        Assert.Equal(BudgetSheetLayout.FreeRowCount, layout.Rows.Count - 11);
        Assert.Equal("F.1", layout.Rows[11].Key);
        Assert.Equal(Enumerable.Range(1, layout.Rows.Count), layout.Rows.Select(r => r.Number));
        Assert.Equal((BudgetRowKind.Subtotal, "ConsumerSpending", (string?)null, "USD"), (layout.Rows[8].Kind, layout.Rows[8].MainClass, layout.Rows[8].SubClass, layout.Rows[8].Currency));
    }

    /// <summary>An empty sheet has only the free rows.</summary>
    [Fact]
    public void AnEmptySheet_HasOnlyFreeRows() => Assert.All(Layout().Rows, r => Assert.Equal(BudgetRowKind.Free, r.Kind));

    /// <summary>Rows are found by key and by number.</summary>
    [Fact]
    public void Rows_AreFoundByKeyAndNumber()
    {
        BudgetSheetLayout layout = Layout(L("ConsumerSpending", "PinMoney"));

        Assert.Equal(2, layout.Row("L.ConsumerSpending.PinMoney.4B5257")!.Number);
        Assert.Equal("S.ConsumerSpending.4B5257", layout.RowAt(3)!.Key);
        Assert.Null(layout.Row("L.Nope"));
        Assert.Null(layout.RowAt(0));
        Assert.Null(layout.RowAt(999));
    }

    /// <summary>What each cell is: column A labels (free in free rows), budget cells, computed totals, blank header cells, free columns O..Z.</summary>
    [Theory]
    [InlineData("H.ConsumerSpending", "A", BudgetCellRole.Label)]
    [InlineData("H.ConsumerSpending", "B", BudgetCellRole.Blank)]
    [InlineData("H.ConsumerSpending", "N", BudgetCellRole.Blank)]
    [InlineData("H.ConsumerSpending", "O", BudgetCellRole.Free)]
    [InlineData("L.ConsumerSpending.PinMoney.4B5257", "A", BudgetCellRole.Label)]
    [InlineData("L.ConsumerSpending.PinMoney.4B5257", "B", BudgetCellRole.Budget)]
    [InlineData("L.ConsumerSpending.PinMoney.4B5257", "M", BudgetCellRole.Budget)]
    [InlineData("L.ConsumerSpending.PinMoney.4B5257", "N", BudgetCellRole.Computed)]
    [InlineData("L.ConsumerSpending.PinMoney.4B5257", "Z", BudgetCellRole.Free)]
    [InlineData("S.ConsumerSpending.4B5257", "B", BudgetCellRole.Computed)]
    [InlineData("S.ConsumerSpending.4B5257", "N", BudgetCellRole.Computed)]
    [InlineData("T.4B5257", "G", BudgetCellRole.Computed)]
    [InlineData("T.4B5257", "P", BudgetCellRole.Free)]
    [InlineData("F.1", "A", BudgetCellRole.Free)]
    [InlineData("F.30", "N", BudgetCellRole.Free)]
    public void Roles_AreFixedByRowAndColumn(string key, string column, BudgetCellRole role) =>
        Assert.Equal(role, Layout(L("ConsumerSpending", "PinMoney")).Role(key, column));

    /// <summary>A cell outside the sheet has no role.</summary>
    [Fact]
    public void ACellOutsideTheSheet_HasNoRole()
    {
        Assert.Null(Layout().Role("L.Nope", "B"));
        Assert.Null(Layout().Role("F.1", "AA"));
    }

    /// <summary>
    /// The computed cells' formulas (stored form): a line's year total sums its months; a subtotal sums its main class's lines of its
    /// currency; a total sums the subtotals of its currency.
    /// </summary>
    [Fact]
    public void ComputedCells_HaveTheirSums()
    {
        BudgetSheetLayout layout = Layout(L("ConsumerSpending", "PinMoney"), L("ConsumerSpending", "MealOrEatOutExpenses"), L("ConsumerSpending", "LeisureOrCulture", "USD"),
            L("RegularSavings", "Deposit"));

        Assert.Equal("=SUM(B{L.ConsumerSpending.PinMoney.4B5257}:M{L.ConsumerSpending.PinMoney.4B5257})", layout.ComputedFormula("L.ConsumerSpending.PinMoney.4B5257", "N"));
        Assert.Equal("=SUM(C{L.ConsumerSpending.MealOrEatOutExpenses.4B5257},C{L.ConsumerSpending.PinMoney.4B5257})", layout.ComputedFormula("S.ConsumerSpending.4B5257", "C"));
        Assert.Equal("=SUM(N{S.RegularSavings.4B5257},N{S.ConsumerSpending.4B5257})", layout.ComputedFormula("T.4B5257", "N"));
        Assert.Null(layout.ComputedFormula("L.ConsumerSpending.PinMoney.4B5257", "B"));
        Assert.Null(layout.ComputedFormula("F.1", "N"));
    }

    /// <summary>A budget cell's month is its column (B = January .. M = December); other columns have none.</summary>
    [Theory]
    [InlineData("B", 1)]
    [InlineData("M", 12)]
    [InlineData("N", null)]
    [InlineData("A", null)]
    public void MonthOf_IsTheColumn(string column, int? month) => Assert.Equal(month, BudgetSheetLayout.MonthOf(column));

    /// <summary>The line of a row, for a line row only.</summary>
    [Fact]
    public void LineOf_IsTheRowsLine()
    {
        BudgetLine line = L("ConsumerSpending", "PinMoney");
        BudgetSheetLayout layout = Layout(line);

        Assert.Equal(line, layout.LineOf(line.Key));
        Assert.Null(layout.LineOf("S.ConsumerSpending.4B5257"));
    }

    /// <summary>The A1 address of a cell and back.</summary>
    [Fact]
    public void Addresses_TranslateBothWays()
    {
        BudgetSheetLayout layout = Layout(L("ConsumerSpending", "PinMoney"));

        Assert.Equal("B2", layout.A1("L.ConsumerSpending.PinMoney.4B5257", "B"));
        Assert.Equal(("L.ConsumerSpending.PinMoney.4B5257", "B"), layout.Locate("b2"));
        Assert.Null(layout.Locate("B999"));
        Assert.Null(layout.Locate("AA1"));
        Assert.Null(layout.Locate("nonsense"));
    }

    /// <summary>A row says which of its columns can be edited, and that agrees with the cell roles (budget and free cells).</summary>
    [Fact]
    public void EditableColumns_AgreeWithTheRoles()
    {
        BudgetSheetLayout layout = Layout(L("ConsumerSpending", "PinMoney"));

        Assert.Equal("OPQRSTUVWXYZ", layout.Row("H.ConsumerSpending")!.EditableColumns);
        Assert.Equal("BCDEFGHIJKLMOPQRSTUVWXYZ", layout.Row("L.ConsumerSpending.PinMoney.4B5257")!.EditableColumns);
        Assert.Equal("OPQRSTUVWXYZ", layout.Row("T.4B5257")!.EditableColumns);
        Assert.Equal("ABCDEFGHIJKLMNOPQRSTUVWXYZ", layout.Row("F.1")!.EditableColumns);
        foreach (BudgetRow row in layout.Rows)
            foreach (char column in "ABCDEFGHIJKLMNOPQRSTUVWXYZ")
                Assert.Equal(layout.Role(row.Key, column.ToString()) is BudgetCellRole.Budget or BudgetCellRole.Free, row.EditableColumns.Contains(column, StringComparison.Ordinal));
    }
}
