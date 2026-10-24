using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Budgets;
using Maroik.Core.Service.Tests.Fixtures;
using H = Maroik.Core.Service.Tests.Fixtures.BudgetServiceHarness;

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// Tests for the reads of <see cref="Maroik.Core.Service.Services.BudgetService"/>: a sheet shown from its stored lines and inputs with
/// the year's actuals, a sheet not stored yet shown with its default lines, the formula preview, and the refusals.
/// </summary>
public class BudgetServiceReadTests
{
    /// <summary>The harness.</summary>
    private readonly BudgetServiceHarness _h = new();

    /// <summary>Meal / eat-out in KRW.</summary>
    private static readonly BudgetLine Meal = BudgetLine.Create(BudgetKind.Expenditure, "ConsumerSpending", "MealOrEatOutExpenses", "KRW").Value;

    /// <summary>The cell of a sheet at an address.</summary>
    private static BudgetCellResponse Cell(BudgetSheetResponse sheet, string address) => sheet.Cells.Single(c => c.Address == address);

    /// <summary>A stored sheet is shown with its inputs calculated and its budget cells against the year's actuals.</summary>
    [Fact]
    public async Task GetSheetAsync_ShowsAStoredSheet()
    {
        long id = _h.Sheet(BudgetKind.Expenditure, 2026, H.Email, Meal);
        _h.Cell(id, Meal.Key, "E", "100000");
        _h.Cell(id, Meal.Key, "F", $"=E{{{Meal.Key}}}*1.5");
        _h.Spend(Meal, 120000m, new DateTime(2026, 4, 10));
        _h.Spend(Meal, 99m, new DateTime(2025, 4, 10));

        var (result, sheet) = await _h.Sut.GetSheetAsync(H.Email, BudgetKind.Expenditure, 2026, "UTC", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(("Expenditure", 2026, true), (sheet!.Kind, sheet.Year, sheet.Exists));
        Assert.Equal(("Line", "ConsumerSpending", "MealOrEatOutExpenses", "KRW", "BCDEFGHIJKLMOPQRSTUVWXYZ"),
            (sheet.Rows[1].Kind, sheet.Rows[1].MainClass, sheet.Rows[1].SubClass, sheet.Rows[1].Currency, sheet.Rows[1].EditableColumns));
        BudgetCellResponse april = Cell(sheet, "E2");
        Assert.Equal(("Budget", "100000", 100000m, 120000m, -20000m, "Over"), (april.Role, april.Input, april.Budget, april.Actual, april.Remaining, april.State));
        Assert.Equal(("Number", 150000m), (Cell(sheet, "F2").Value.Kind, Cell(sheet, "F2").Value.Number));
        Assert.Equal("=E2*1.5", Cell(sheet, "F2").Input);
        _h.AssertTransactions(0, 0);
    }

    /// <summary>A sheet not stored yet shows one line per class in the account's default currency, and nothing is stored.</summary>
    [Fact]
    public async Task GetSheetAsync_ShowsAnAbsentSheetWithItsDefaultLines()
    {
        var (_, sheet) = await _h.Sut.GetSheetAsync(H.Email, BudgetKind.Income, 2030, "UTC", TestContext.Current.CancellationToken);

        Assert.False(sheet!.Exists);
        Assert.Equal(8, sheet.Rows.Count(r => r.Kind == "Line"));
        Assert.All(sheet.Rows.Where(r => r.Kind == "Line"), r => Assert.Equal("KRW", r.Currency));
        Assert.Empty(_h.Sheets.All);
    }

    /// <summary>Another account's sheet is never shown.</summary>
    [Fact]
    public async Task GetSheetAsync_IgnoresOtherAccounts()
    {
        long other = _h.Sheet(BudgetKind.Expenditure, 2026, "other@example.com", Meal);
        _h.Cell(other, Meal.Key, "B", "5");

        var (_, sheet) = await _h.Sut.GetSheetAsync(H.Email, BudgetKind.Expenditure, 2026, "UTC", TestContext.Current.CancellationToken);

        Assert.False(sheet!.Exists);
        Assert.DoesNotContain(sheet.Cells, c => c.Input == "5");
    }

    /// <summary>A year outside 1..9999 is refused with the domain's message.</summary>
    [Fact]
    public async Task GetSheetAsync_RefusesABadYear()
    {
        var (result, sheet) = await _h.Sut.GetSheetAsync(H.Email, BudgetKind.Income, 0, "UTC", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(("Budget.YearInvalid", "The year must be between {0} and {1}.", ServiceErrorType.Validation), (result.ErrorCode, result.ErrorKey, result.ErrorType));
        Assert.Null(sheet);
    }

    /// <summary>A formula reaching a sheet that is not stored reads it as its default lines (empty), not #REF!.</summary>
    [Fact]
    public async Task GetSheetAsync_LoadsReferencedSheetsThatAreNotStored()
    {
        long id = _h.Sheet(BudgetKind.Expenditure, 2026, H.Email, Meal);
        _h.Cell(id, "F.1", "B", "=Income2030!N{L.RegularIncome.LaborIncome.4B5257}+1");

        var (_, sheet) = await _h.Sut.GetSheetAsync(H.Email, BudgetKind.Expenditure, 2026, "UTC", TestContext.Current.CancellationToken);

        Assert.Equal(("Number", 1m), (Cell(sheet!, "B5").Value.Kind, Cell(sheet!, "B5").Value.Number));
    }

    /// <summary>Records of a line the sheet does not have are listed.</summary>
    [Fact]
    public async Task GetSheetAsync_ListsUnbudgetedRecords()
    {
        _h.Sheet(BudgetKind.Expenditure, 2026, H.Email, Meal);
        _h.Spend(BudgetLine.Create(BudgetKind.Expenditure, "ConsumerSpending", "PinMoney", "USD").Value, 7m, new DateTime(2026, 2, 1));

        var (_, sheet) = await _h.Sut.GetSheetAsync(H.Email, BudgetKind.Expenditure, 2026, "UTC", TestContext.Current.CancellationToken);

        BudgetUnbudgetedResponse line = Assert.Single(sheet!.Unbudgeted);
        Assert.Equal(("ConsumerSpending", "PinMoney", "USD", 7m), (line.MainClass, line.SubClass, line.Currency, line.Months[1]));
    }

    /// <summary>The preview colours the input, lists its problems with their message keys, and shows the value it would have.</summary>
    [Fact]
    public async Task PreviewAsync_ColoursAndCalculates()
    {
        long id = _h.Sheet(BudgetKind.Expenditure, 2026, H.Email, Meal);
        _h.Cell(id, Meal.Key, "B", "40");

        var (result, preview) = await _h.Sut.PreviewAsync(H.Email, BudgetKind.Expenditure, 2026, "UTC", "C2", "=B2*2+FOO(1)", TestContext.Current.CancellationToken);
        var (_, ok) = await _h.Sut.PreviewAsync(H.Email, BudgetKind.Expenditure, 2026, "UTC", "C2", "=B2*2", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(["operator", "reference", "operator", "number", "operator", "unknown-function", "paren", "number", "paren"], preview!.Tokens.Select(t => t.Class));
        Assert.Equal(("B2", "B2"), (preview.References[0].TopLeft, preview.References[0].BottomRight));
        BudgetProblemResponse problem = Assert.Single(preview.Problems);
        Assert.Equal((6, 3, "There is no function named {0}.", false), (problem.Start, problem.Length, problem.ErrorKey, problem.Blocking));
        Assert.Equal(["FOO"], problem.ErrorArgs);
        Assert.True(preview.CanSave);
        Assert.Equal(("Error", "#NAME?"), (preview.Value!.Kind, preview.Value.Text));
        Assert.Equal(80m, ok!.Value!.Number);
    }

    /// <summary>The preview refuses a bad year too.</summary>
    [Fact]
    public async Task PreviewAsync_RefusesABadYear() =>
        Assert.False((await _h.Sut.PreviewAsync(H.Email, BudgetKind.Income, 10000, "UTC", "B2", "=1", TestContext.Current.CancellationToken)).Result.Success);
}
