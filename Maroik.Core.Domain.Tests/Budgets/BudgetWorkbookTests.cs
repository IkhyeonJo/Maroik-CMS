using Maroik.Core.Domain.Budgets;
using Maroik.Core.Domain.Finances;
using Maroik.Core.Domain.Formulas;

namespace Maroik.Core.Domain.Tests.Budgets;

/// <summary>
/// Unit tests for <see cref="BudgetWorkbook"/>: the sheets of an account calculated together — computed totals, budget against actual,
/// unbudgeted records — and the edits: a cell edit recalculated reactively, paste / fill with relative references, the formula preview
/// and the carry-over from last year.
/// </summary>
public class BudgetWorkbookTests
{
    /// <summary>Meal / eat-out in KRW (row 2 of the expenditure sheet below).</summary>
    private static readonly BudgetLine Meal = BudgetLine.Create(BudgetKind.Expenditure, "ConsumerSpending", "MealOrEatOutExpenses", "KRW").Value;

    /// <summary>Pin money in KRW (row 3).</summary>
    private static readonly BudgetLine Pin = BudgetLine.Create(BudgetKind.Expenditure, "ConsumerSpending", "PinMoney", "KRW").Value;

    /// <summary>Labor income in KRW (row 2 of the income sheet).</summary>
    private static readonly BudgetLine Labor = BudgetLine.Create(BudgetKind.Income, "RegularIncome", "LaborIncome", "KRW").Value;

    // Expenditure2026: 1 header, 2 Meal, 3 Pin, 4 subtotal, 5 total, 6.. free rows (F.1 = row 6).

    /// <summary>A sheet's data with inputs written as ("B" + key) → stored input.</summary>
    private static BudgetSheetData Sheet(BudgetKind kind, int year, BudgetLine[] lines, params (string Key, string Column, string Input)[] inputs) =>
        new(kind, year, true, lines, inputs.ToDictionary(i => new BudgetCellKey(i.Key, i.Column), i => i.Input));

    /// <summary>Actuals with expenditure records (UTC timestamps, UTC zone).</summary>
    private static BudgetActuals Spent(params (BudgetLine Line, int Month, decimal Amount)[] records) =>
        BudgetActuals.From([], records.Select(r => Expenditure.Reconstitute(1, "u@example.com", r.Line.MainClass, r.Line.SubClass, "c", r.Amount, r.Line.Currency, "card", null, null,
            new DateTime(2026, r.Month, 10), new DateTime(2026, r.Month, 10))), "UTC");

    /// <summary>A workbook of one expenditure sheet (Meal, Pin) with the given inputs and actuals.</summary>
    private static BudgetWorkbook Workbook(BudgetActuals? actuals = null, params (string Key, string Column, string Input)[] inputs) =>
        new([Sheet(BudgetKind.Expenditure, 2026, [Meal, Pin], inputs)], actuals ?? Spent());

    /// <summary>The cell of a view at an address.</summary>
    private static BudgetCellView Cell(BudgetSheetView view, string address) => view.Cells.Single(c => c.Address == address);

    /// <summary>A number.</summary>
    private static NumberValue N(decimal value) => new(value);

    /// <summary>The view lays out the rows and calculates the inputs, the line year totals, the subtotals and the totals.</summary>
    [Fact]
    public void TheView_CalculatesTheSheet()
    {
        BudgetSheetView view = Workbook(null, (Meal.Key, "B", "300000"), (Meal.Key, "C", $"=B{{{Meal.Key}}}*1.05"), (Pin.Key, "B", "1000")).View(BudgetKind.Expenditure, 2026);

        Assert.Equal(("H.ConsumerSpending", "F.1"), (view.Rows[0].Key, view.Rows[5].Key));
        Assert.Equal(N(315000), Cell(view, "C2").Value);
        Assert.Equal("=B2*1.05", Cell(view, "C2").Input);
        Assert.Equal(N(615000), Cell(view, "N2").Value);
        Assert.Equal("=SUM(B2:M2)", Cell(view, "N2").Input);
        Assert.Equal(N(301000), Cell(view, "B4").Value);
        Assert.Equal(N(616000), Cell(view, "N5").Value);
        Assert.Equal(BudgetCellRole.Computed, Cell(view, "N5").Role);
        Assert.True(view.Exists);
    }

    /// <summary>Every budget cell carries its budget, the actual total, what remains and its state.</summary>
    [Fact]
    public void BudgetCells_CompareWithTheActuals()
    {
        BudgetSheetView view = Workbook(Spent((Meal, 5, 150000m), (Pin, 1, 5m), (Meal, 2, 100m)), (Meal.Key, "F", "100000"), (Meal.Key, "B", "200"), (Meal.Key, "C", "100"))
            .View(BudgetKind.Expenditure, 2026);

        Assert.Equal(new BudgetCellStatus(100000m, 150000m, -50000m, BudgetCellState.Over), Cell(view, "F2").Status);
        Assert.Equal(new BudgetCellStatus(200m, 0m, 200m, BudgetCellState.Under), Cell(view, "B2").Status);
        Assert.Equal(new BudgetCellStatus(100m, 100m, 0m, BudgetCellState.Reached), Cell(view, "C2").Status);
        Assert.Equal(new BudgetCellStatus(null, 5m, null, BudgetCellState.Unbudgeted), Cell(view, "B3").Status);
        Assert.Equal(new BudgetCellStatus(null, 0m, null, BudgetCellState.None), Cell(view, "D3").Status);
        Assert.Null(Cell(view, "N2").Status);
    }

    /// <summary>A budget below zero or beyond the amount range is #NUM!; a budget is rounded to four decimals; text is no budget.</summary>
    [Fact]
    public void BudgetValues_AreChecked()
    {
        BudgetSheetView view = Workbook(null, (Meal.Key, "B", "=0-1"), (Meal.Key, "C", "=1.23456"), (Meal.Key, "D", "=\"x\""), (Meal.Key, "E", "=9E16*10"))
            .View(BudgetKind.Expenditure, 2026);

        Assert.Equal(new ErrorValue(FormulaErrorKind.Num), Cell(view, "B2").Value);
        Assert.Equal(N(1.2346m), Cell(view, "C2").Value);
        Assert.Null(Cell(view, "D2").Status!.Budget);
        Assert.Equal(new ErrorValue(FormulaErrorKind.Num), Cell(view, "E2").Value);
    }

    /// <summary>Records of a line the sheet does not have are listed as unbudgeted, with their monthly totals.</summary>
    [Fact]
    public void RecordsWithoutALine_AreListed()
    {
        BudgetLine taxi = BudgetLine.Create(BudgetKind.Expenditure, "ConsumerSpending", "TransportationCost", "USD").Value;

        BudgetSheetView view = Workbook(Spent((taxi, 3, 12m), (Meal, 3, 1m))).View(BudgetKind.Expenditure, 2026);

        BudgetUnbudgetedLine line = Assert.Single(view.Unbudgeted);
        Assert.Equal(taxi, line.Line);
        Assert.Equal(12m, line.Months[2]);
    }

    /// <summary>An edit is stored canonically and recalculates what it reaches; the reply lists the open sheet's changed cells.</summary>
    [Fact]
    public void AnEdit_RecalculatesWhatItReaches()
    {
        BudgetWorkbook workbook = Workbook(null, (Meal.Key, "C", $"=B{{{Meal.Key}}}*2"), ("F.1", "B", "7"));

        BudgetEdit edit = workbook.Edit(BudgetKind.Expenditure, 2026, [new BudgetCellEdit("B2", "1,000")]).Value;

        Assert.Equal([new BudgetCellWrite(Meal.Key, "B", "1000")], edit.Writes);
        Assert.Equal(["B2", "B4", "B5", "C2", "C4", "C5", "N2", "N4", "N5"], edit.Changed.Select(c => c.Address).Order(StringComparer.Ordinal));
        Assert.Equal(N(2000), Cell(workbook.View(BudgetKind.Expenditure, 2026), "C2").Value);
    }

    /// <summary>An edited cell is in the reply even when its value stays the same (its input changed).</summary>
    [Fact]
    public void AnEditedCell_IsAlwaysInTheReply()
    {
        BudgetWorkbook workbook = Workbook(null, ("F.1", "B", "=1"));

        BudgetEdit edit = workbook.Edit(BudgetKind.Expenditure, 2026, [new BudgetCellEdit("B6", "1")]).Value;

        BudgetCellView cell = Assert.Single(edit.Changed);
        Assert.Equal(("B6", "1"), (cell.Address, cell.Input));
    }

    /// <summary>Clearing a cell writes no input.</summary>
    [Fact]
    public void Clearing_WritesNothing()
    {
        BudgetEdit edit = Workbook(null, (Meal.Key, "B", "5")).Edit(BudgetKind.Expenditure, 2026, [new BudgetCellEdit("B2", "")]).Value;

        Assert.Equal([new BudgetCellWrite(Meal.Key, "B", null)], edit.Writes);
    }

    /// <summary>An edit the cell does not accept, or of a cell the sheet does not have, is refused and changes nothing.</summary>
    [Fact]
    public void BadEdits_AreRefused()
    {
        BudgetWorkbook workbook = Workbook(null, (Meal.Key, "B", "5"));

        Assert.Equal("Budget.CellNeedsNumber", workbook.Edit(BudgetKind.Expenditure, 2026, [new BudgetCellEdit("C2", "1"), new BudgetCellEdit("B2", "lunch")]).FirstError.Code);
        Assert.Equal("Budget.CellReadOnly", workbook.Edit(BudgetKind.Expenditure, 2026, [new BudgetCellEdit("N2", "1")]).FirstError.Code);
        Assert.Equal("Budget.CellUnknown", workbook.Edit(BudgetKind.Expenditure, 2026, [new BudgetCellEdit("B999", "1")]).FirstError.Code);
        Assert.Equal("Budget.SheetUnknown", workbook.Edit(BudgetKind.Income, 2026, [new BudgetCellEdit("B2", "1")]).FirstError.Code);
        Assert.Equal(EmptyValue.Instance, Cell(workbook.View(BudgetKind.Expenditure, 2026), "C2").Value);
    }

    /// <summary>A formula reads another sheet of the workbook; an edit there reaches it.</summary>
    [Fact]
    public void Formulas_ReadOtherSheets()
    {
        BudgetWorkbook workbook = new([
            Sheet(BudgetKind.Expenditure, 2026, [Meal], ("F.1", "B", "=Income!N{" + Labor.Key + "}*0.2")),
            Sheet(BudgetKind.Income, 2026, [Labor], (Labor.Key, "B", "1000")),
        ], Spent());
        // Expenditure2026 with Meal only: 1 header, 2 Meal, 3 subtotal, 4 total, 5 = F.1.

        Assert.Equal(N(200), Cell(workbook.View(BudgetKind.Expenditure, 2026), "B5").Value);
        Assert.Equal("=Income!N2*0.2", Cell(workbook.View(BudgetKind.Expenditure, 2026), "B5").Input);

        workbook.Edit(BudgetKind.Income, 2026, [new BudgetCellEdit("B2", "2000")]);
        Assert.Equal(N(400), Cell(workbook.View(BudgetKind.Expenditure, 2026), "B5").Value);
    }

    /// <summary>A reference to a sheet the workbook does not hold, or to a removed line, is #REF!.</summary>
    [Fact]
    public void MissingSheetsAndLines_AreRef()
    {
        BudgetSheetView view = Workbook(null, ("F.1", "B", "=Income2020!B{F.1}"), ("F.1", "C", "=B{L.ConsumerSpending.Gone.4B5257}"), ("L.ConsumerSpending.Gone.4B5257", "B", "9"))
            .View(BudgetKind.Expenditure, 2026);

        Assert.Equal(new ErrorValue(FormulaErrorKind.Ref), Cell(view, "B6").Value);
        Assert.Equal("=#REF!", Cell(view, "C6").Input);
        Assert.DoesNotContain(view.Cells, c => c.Input == "9");
    }

    /// <summary>The cells of a circular reference are #CYCLE!.</summary>
    [Fact]
    public void ACycle_IsShown()
    {
        BudgetSheetView view = Workbook(null, ("F.1", "B", "=C{F.1}"), ("F.1", "C", "=B{F.1}+1")).View(BudgetKind.Expenditure, 2026);

        Assert.Equal(new ErrorValue(FormulaErrorKind.Cycle), Cell(view, "B6").Value);
    }

    /// <summary>Fill right copies a formula across, its relative references moving with it.</summary>
    [Fact]
    public void FillRight_MovesRelativeReferences()
    {
        BudgetWorkbook workbook = Workbook(null, (Meal.Key, "B", "100"), (Meal.Key, "C", $"=B{{{Meal.Key}}}*2"));

        BudgetEdit edit = workbook.Copy(BudgetKind.Expenditure, 2026, "C2", "D2:E2").Value;

        Assert.Equal([new BudgetCellWrite(Meal.Key, "D", $"=C{{{Meal.Key}}}*2"), new BudgetCellWrite(Meal.Key, "E", $"=D{{{Meal.Key}}}*2")], edit.Writes);
        Assert.Equal(N(800), Cell(workbook.View(BudgetKind.Expenditure, 2026), "E2").Value);
    }

    /// <summary>A block is tiled over a target that is a multiple of it; otherwise it is pasted once at the target's corner.</summary>
    [Fact]
    public void Paste_TilesOrPastesOnce()
    {
        Assert.Equal(["D", "E", "F", "G"], Workbook(null, (Meal.Key, "B", "1"), (Meal.Key, "C", "2")).Copy(BudgetKind.Expenditure, 2026, "B2:C2", "D2:G2").Value.Writes.Select(w => w.Column));
        Assert.Equal(["D", "E"], Workbook(null, (Meal.Key, "B", "1"), (Meal.Key, "C", "2")).Copy(BudgetKind.Expenditure, 2026, "B2:C2", "D2:F2").Value.Writes.Select(w => w.Column));
        Assert.Equal(["O", "P"], Workbook(null, (Meal.Key, "B", "1"), (Meal.Key, "C", "2")).Copy(BudgetKind.Expenditure, 2026, "b2:c2", "O6").Value.Writes.Select(w => w.Column));
    }

    /// <summary>A computed cell copies its formula; a label or blank cell copies as empty.</summary>
    [Fact]
    public void Copy_TakesComputedFormulas()
    {
        BudgetEdit edit = Workbook(null).Copy(BudgetKind.Expenditure, 2026, "N2:N3", "O2:O3").Value;

        Assert.Equal([$"=SUM(C{{{Meal.Key}}}:N{{{Meal.Key}}})", $"=SUM(C{{{Pin.Key}}}:N{{{Pin.Key}}})"], edit.Writes.Select(w => w.Input));
        Assert.Equal([null], Workbook(null).Copy(BudgetKind.Expenditure, 2026, "A1", "O6").Value.Writes.Select(w => w.Input));
    }

    /// <summary>A paste the target cells do not accept, that runs off the sheet, or with bad ranges, is refused.</summary>
    [Fact]
    public void BadPastes_AreRefused()
    {
        BudgetWorkbook workbook = Workbook(null, ("F.1", "O", "note"), (Meal.Key, "B", "1"));

        Assert.Equal("Budget.CellNeedsNumber", workbook.Copy(BudgetKind.Expenditure, 2026, "O6", "C2").FirstError.Code);
        Assert.Equal("Budget.CellReadOnly", workbook.Copy(BudgetKind.Expenditure, 2026, "B2", "N2").FirstError.Code);
        Assert.Equal("Budget.PasteOutside", workbook.Copy(BudgetKind.Expenditure, 2026, "B2:C2", "Z6").FirstError.Code);
        Assert.Equal("Budget.CellUnknown", workbook.Copy(BudgetKind.Expenditure, 2026, "B2:", "C2").FirstError.Code);
        Assert.Equal("Budget.CellUnknown", workbook.Copy(BudgetKind.Expenditure, 2026, "B2", "C2:C999").FirstError.Code);
    }

    /// <summary>The preview colours the typed formula and shows the value it would have, #CYCLE! when it reads its own cell.</summary>
    [Fact]
    public void ThePreview_ShowsTheValue()
    {
        BudgetWorkbook workbook = Workbook(null, (Meal.Key, "B", "100"));

        BudgetFormulaPreview ok = workbook.Preview(BudgetKind.Expenditure, 2026, "C2", "=B2*3");
        BudgetFormulaPreview broken = workbook.Preview(BudgetKind.Expenditure, 2026, "C2", "=B2*");
        BudgetFormulaPreview self = workbook.Preview(BudgetKind.Expenditure, 2026, "B6", "=B6+1");
        BudgetFormulaPreview value = workbook.Preview(BudgetKind.Expenditure, 2026, "B6", "12");

        Assert.Equal(N(300), ok.Value);
        Assert.Equal(("B2", "B2"), (ok.Highlight.References[0].TopLeft, ok.Highlight.References[0].BottomRight));
        Assert.Null(broken.Value);
        Assert.False(broken.Highlight.CanSave);
        Assert.Equal(new ErrorValue(FormulaErrorKind.Cycle), self.Value);
        Assert.Null(value.Value);
        Assert.Empty(value.Highlight.Tokens);
    }

    /// <summary>Carry-over fills this year's empty budget cells with last year's inputs, for the lines both sheets have.</summary>
    [Fact]
    public void CarryOver_FillsEmptyBudgetCells()
    {
        BudgetWorkbook workbook = new([
            Sheet(BudgetKind.Expenditure, 2026, [Meal, Pin], (Meal.Key, "C", "9")),
            Sheet(BudgetKind.Expenditure, 2025, [Meal, Labor with { MainClass = "x" }], (Meal.Key, "B", "1"), (Meal.Key, "C", "2"), (Meal.Key, "D", $"=C{{{Meal.Key}}}*2"), ("F.1", "B", "free")),
        ], Spent());

        BudgetEdit edit = workbook.CarryOver(BudgetKind.Expenditure, 2026).Value;

        Assert.Equal([new BudgetCellWrite(Meal.Key, "B", "1"), new BudgetCellWrite(Meal.Key, "D", $"=C{{{Meal.Key}}}*2")], edit.Writes);
        Assert.Equal(N(18), Cell(workbook.View(BudgetKind.Expenditure, 2026), "D2").Value);
    }

    /// <summary>Carry-over without last year's sheet copies nothing.</summary>
    [Fact]
    public void CarryOver_WithoutLastYear_CopiesNothing() =>
        Assert.Empty(Workbook(null).CarryOver(BudgetKind.Expenditure, 2026).Value.Writes);

    /// <summary>The sheets a formula names, with the year of its own sheet for names without one.</summary>
    [Fact]
    public void SheetsReferencedBy_ReadsTheSheetNames() =>
        Assert.Equal([(BudgetKind.Income, 2026), (BudgetKind.Expenditure, 2025)],
            BudgetWorkbook.SheetsReferencedBy("=Income!B2+expenditure2025!C{F.1}+SUM(B2)+Other!B1", 2026));

    /// <summary>A sheet that does not exist yet still has its lines (the defaults) and says so.</summary>
    [Fact]
    public void AnAbsentSheet_IsShownWithItsLines()
    {
        BudgetSheetView view = new BudgetWorkbook([new BudgetSheetData(BudgetKind.Income, 2027, false, [Labor], new Dictionary<BudgetCellKey, string>())], Spent()).View(BudgetKind.Income, 2027);

        Assert.False(view.Exists);
        Assert.Equal(Labor.Key, view.Rows[1].Key);
    }
}
