using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Budgets;
using Maroik.Core.Service.Tests.Fixtures;
using Microsoft.Extensions.Logging;
using Moq;
using H = Maroik.Core.Service.Tests.Fixtures.BudgetServiceHarness;

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// Tests for the writes of <see cref="Maroik.Core.Service.Services.BudgetService"/>: a sheet created on its first edit (account row locked),
/// each edit in its own transaction with the sheet row locked, inputs stored / changed / deleted, lines added and removed, the
/// carry-over, and the refusals and failures (rolled back, failures logged).
/// </summary>
public class BudgetServiceWriteTests
{
    /// <summary>The harness.</summary>
    private readonly BudgetServiceHarness _h = new();

    /// <summary>Meal / eat-out in KRW.</summary>
    private static readonly BudgetLine Meal = BudgetLine.Create(BudgetKind.Expenditure, "ConsumerSpending", "MealOrEatOutExpenses", "KRW").Value;

    /// <summary>Pin money in USD.</summary>
    private static readonly BudgetLine Pin = BudgetLine.Create(BudgetKind.Expenditure, "ConsumerSpending", "PinMoney", "USD").Value;

    /// <summary>The first edit of a sheet stores the sheet with its default lines (account row locked), then the edit (sheet row locked).</summary>
    [Fact]
    public async Task EditAsync_CreatesTheSheetOnItsFirstEdit()
    {
        var (result, edit) = await _h.Sut.EditAsync(H.Email, BudgetKind.Income, 2026, "UTC", [new BudgetCellEdit("B2", "3,000,000")], TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        BudgetSheet sheet = Assert.Single(_h.Sheets.All);
        Assert.Equal((BudgetKind.Income, 2026, H.Now), (sheet.Kind, sheet.Year, sheet.Updated));
        Assert.Equal(8, _h.Lines.All.Count);
        BudgetSheetCell cell = Assert.Single(_h.Cells.All);
        Assert.Equal(("L.RegularIncome.LaborIncome.4B5257", "B", "3000000"), (cell.RowKey, cell.Column, cell.Input));
        Assert.Contains(edit!.Changed, c => c.Address == "B2" && c.Value.Number == 3000000m);
        Assert.Equal(1, edit.Written);
        _h.Accounts.Verify(a => a.FindByEmailForUpdateAsync(H.Email, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal([(H.Email, BudgetKind.Income, 2026)], _h.Sheets.Locks);
        _h.AssertTransactions(2, 2);
    }

    /// <summary>An edit of a stored sheet changes, adds and deletes inputs (clearing an empty cell does nothing) in one transaction with the sheet row locked.</summary>
    [Fact]
    public async Task EditAsync_ChangesAddsAndDeletesInputs()
    {
        long id = _h.Sheet(BudgetKind.Expenditure, 2026, H.Email, Meal);
        _h.Cell(id, Meal.Key, "B", "1");
        _h.Cell(id, Meal.Key, "C", "2");

        var (result, _) = await _h.Sut.EditAsync(H.Email, BudgetKind.Expenditure, 2026, "UTC",
            [new BudgetCellEdit("B2", "10"), new BudgetCellEdit("C2", ""), new BudgetCellEdit("D2", "=B2*2"), new BudgetCellEdit("E2", "")], TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal([("B", "10", H.Now), ("D", $"=B{{{Meal.Key}}}*2", H.Now)], _h.Cells.All.OrderBy(c => c.Column, StringComparer.Ordinal).Select(c => (c.Column, c.Input, c.Updated)));
        Assert.Equal(H.Now, _h.Sheets.All.Single().Updated);
        _h.Accounts.Verify(a => a.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _h.AssertTransactions(1, 1);
    }

    /// <summary>An edit the cell refuses is rolled back with the domain's message and stores nothing.</summary>
    [Fact]
    public async Task EditAsync_RefusesABadInput()
    {
        _h.Sheet(BudgetKind.Expenditure, 2026, H.Email, Meal);

        var (result, edit) = await _h.Sut.EditAsync(H.Email, BudgetKind.Expenditure, 2026, "UTC", [new BudgetCellEdit("B2", "lunch")], TestContext.Current.CancellationToken);

        Assert.Equal(("Budget.CellNeedsNumber", "A budget cell takes a number or a formula.", ServiceErrorType.Validation), (result.ErrorCode, result.ErrorKey, result.ErrorType));
        Assert.Null(edit);
        Assert.Empty(_h.Cells.All);
        _h.AssertTransactions(1, 0);
    }

    /// <summary>A failure while storing is logged with its exception, rolled back and answered with the temporary error.</summary>
    [Fact]
    public async Task EditAsync_LogsAndRollsBackAFailure()
    {
        _h.Sheet(BudgetKind.Expenditure, 2026, H.Email, Meal);
        var boom = new InvalidOperationException("db down");
        _h.Cells.FailWrites = boom;

        var (result, _) = await _h.Sut.EditAsync(H.Email, BudgetKind.Expenditure, 2026, "UTC", [new BudgetCellEdit("B2", "1")], TestContext.Current.CancellationToken);

        Assert.Equal((ServiceErrorKeys.TemporaryError, ServiceErrorType.Failure), (result.ErrorKey, result.ErrorType));
        var log = _h.Logger.LatestRecord;
        Assert.Equal((LogLevel.Error, boom), (log.Level, log.Exception));
        Assert.Equal("Budget: failed to edit Expenditure2026 of user@example.com", log.Message);
        _h.AssertTransactions(1, 0);
    }

    /// <summary>A failure while creating the sheet is logged and rolled back too.</summary>
    [Fact]
    public async Task EditAsync_LogsAFailureCreatingTheSheet()
    {
        var boom = new InvalidOperationException("db down");
        _h.Accounts.Setup(a => a.FindByEmailForUpdateAsync(H.Email, It.IsAny<CancellationToken>())).ThrowsAsync(boom);

        var (result, _) = await _h.Sut.EditAsync(H.Email, BudgetKind.Income, 2026, "UTC", [new BudgetCellEdit("B2", "1")], TestContext.Current.CancellationToken);

        Assert.Equal(ServiceErrorKeys.TemporaryError, result.ErrorKey);
        Assert.Equal(("Budget: failed to create Income2026 of user@example.com", boom), (_h.Logger.LatestRecord.Message, _h.Logger.LatestRecord.Exception));
        Assert.Empty(_h.Sheets.All);
        _h.AssertTransactions(1, 0);
    }

    /// <summary>A sheet another request created meanwhile is used, not created twice.</summary>
    [Fact]
    public async Task EditAsync_UsesASheetCreatedMeanwhile()
    {
        _h.Accounts.Setup(a => a.FindByEmailForUpdateAsync(H.Email, It.IsAny<CancellationToken>()))
            .Callback(() => _h.Sheet(BudgetKind.Income, 2026, H.Email))
            .ReturnsAsync((Maroik.Core.Domain.Accounts.Account?)null);

        var (result, _) = await _h.Sut.EditAsync(H.Email, BudgetKind.Income, 2026, "UTC", [new BudgetCellEdit("B6", "1")], TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Single(_h.Sheets.All);
        _h.AssertTransactions(2, 1);
    }

    /// <summary>Paste / fill stores the moved formulas.</summary>
    [Fact]
    public async Task CopyAsync_StoresTheMovedFormulas()
    {
        long id = _h.Sheet(BudgetKind.Expenditure, 2026, H.Email, Meal);
        _h.Cell(id, Meal.Key, "C", $"=B{{{Meal.Key}}}+1");

        var (result, edit) = await _h.Sut.CopyAsync(H.Email, BudgetKind.Expenditure, 2026, "UTC", "C2", "D2:E2", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(2, edit!.Written);
        Assert.Equal([$"=B{{{Meal.Key}}}+1", $"=C{{{Meal.Key}}}+1", $"=D{{{Meal.Key}}}+1"], _h.Cells.All.OrderBy(c => c.Column, StringComparer.Ordinal).Select(c => c.Input));
        _h.AssertTransactions(1, 1);
    }

    /// <summary>A paste the sheet refuses is rolled back with the domain's message.</summary>
    [Fact]
    public async Task CopyAsync_RefusesABadPaste()
    {
        _h.Sheet(BudgetKind.Expenditure, 2026, H.Email, Meal);

        var (result, _) = await _h.Sut.CopyAsync(H.Email, BudgetKind.Expenditure, 2026, "UTC", "B2", "N2", TestContext.Current.CancellationToken);

        Assert.Equal("Budget.CellReadOnly", result.ErrorCode);
        _h.AssertTransactions(1, 0);
    }

    /// <summary>A failed paste is logged.</summary>
    [Fact]
    public async Task CopyAsync_LogsAFailure()
    {
        long id = _h.Sheet(BudgetKind.Expenditure, 2026, H.Email, Meal);
        _h.Cell(id, Meal.Key, "B", "1");
        _h.Cells.FailWrites = new InvalidOperationException("db down");

        var (result, _) = await _h.Sut.CopyAsync(H.Email, BudgetKind.Expenditure, 2026, "UTC", "B2", "C2", TestContext.Current.CancellationToken);

        Assert.Equal(ServiceErrorKeys.TemporaryError, result.ErrorKey);
        Assert.Equal("Budget: failed to paste into Expenditure2026 of user@example.com", _h.Logger.LatestRecord.Message);
    }

    /// <summary>A line is added to the sheet and shown.</summary>
    [Fact]
    public async Task AddLineAsync_AddsTheLine()
    {
        _h.Sheet(BudgetKind.Expenditure, 2026, H.Email, Meal);

        var (result, sheet) = await _h.Sut.AddLineAsync(H.Email, BudgetKind.Expenditure, 2026, "UTC", "ConsumerSpending", "PinMoney", "usd", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal([Meal, Pin], _h.Lines.All.Select(l => l.Line).OrderBy(l => l.SubClass, StringComparer.Ordinal));
        Assert.Contains(sheet!.Rows, r => r.SubClass == "PinMoney" && r.Currency == "USD");
        _h.AssertTransactions(1, 1);
    }

    /// <summary>A line the sheet has, or that the domain refuses, is not added.</summary>
    [Fact]
    public async Task AddLineAsync_RefusesDuplicatesAndBadLines()
    {
        _h.Sheet(BudgetKind.Expenditure, 2026, H.Email, Meal);

        var (duplicate, _) = await _h.Sut.AddLineAsync(H.Email, BudgetKind.Expenditure, 2026, "UTC", "ConsumerSpending", "MealOrEatOutExpenses", "KRW", TestContext.Current.CancellationToken);
        var (bad, _) = await _h.Sut.AddLineAsync(H.Email, BudgetKind.Expenditure, 2026, "UTC", "RegularIncome", "LaborIncome", "KRW", TestContext.Current.CancellationToken);

        Assert.Equal(("Budget.LineExists", ServiceErrorKeys.BudgetLineExists, ServiceErrorType.Conflict), (duplicate.ErrorCode, duplicate.ErrorKey, duplicate.ErrorType));
        Assert.Equal("ExpenditureClass.Invalid", bad.ErrorCode);
        Assert.Single(_h.Lines.All);
        _h.AssertTransactions(2, 0);
    }

    /// <summary>The first line of a sheet not stored yet creates the sheet with its default lines first.</summary>
    [Fact]
    public async Task AddLineAsync_CreatesTheSheetFirst()
    {
        var (result, _) = await _h.Sut.AddLineAsync(H.Email, BudgetKind.Expenditure, 2027, "UTC", "ConsumerSpending", "PinMoney", "USD", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(21, _h.Lines.All.Count);
        _h.AssertTransactions(2, 2);
    }

    /// <summary>Removing a line removes it and its inputs.</summary>
    [Fact]
    public async Task RemoveLineAsync_RemovesTheLineAndItsInputs()
    {
        long id = _h.Sheet(BudgetKind.Expenditure, 2026, H.Email, Meal, Pin);
        _h.Cell(id, Meal.Key, "B", "1");
        _h.Cell(id, "F.1", "B", "2");

        var (result, sheet) = await _h.Sut.RemoveLineAsync(H.Email, BudgetKind.Expenditure, 2026, "UTC", "ConsumerSpending", "MealOrEatOutExpenses", "KRW", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal([Pin], _h.Lines.All.Select(l => l.Line));
        Assert.Equal(["F.1"], _h.Cells.All.Select(c => c.RowKey));
        Assert.DoesNotContain(sheet!.Rows, r => r.SubClass == "MealOrEatOutExpenses");
        _h.AssertTransactions(1, 1);
    }

    /// <summary>A line the sheet does not have, or a sheet not stored, cannot be removed.</summary>
    [Fact]
    public async Task RemoveLineAsync_RefusesAMissingLine()
    {
        _h.Sheet(BudgetKind.Expenditure, 2026, H.Email, Meal);

        var (missing, _) = await _h.Sut.RemoveLineAsync(H.Email, BudgetKind.Expenditure, 2026, "UTC", "ConsumerSpending", "PinMoney", "USD", TestContext.Current.CancellationToken);
        var (noSheet, _) = await _h.Sut.RemoveLineAsync(H.Email, BudgetKind.Income, 2026, "UTC", "RegularIncome", "LaborIncome", "KRW", TestContext.Current.CancellationToken);

        Assert.Equal(("Budget.LineNotFound", ServiceErrorKeys.BudgetLineNotFound, ServiceErrorType.NotFound), (missing.ErrorCode, missing.ErrorKey, missing.ErrorType));
        Assert.Equal("Budget.LineNotFound", noSheet.ErrorCode);
        _h.AssertTransactions(2, 0);
    }

    /// <summary>A failed line change is logged.</summary>
    [Fact]
    public async Task RemoveLineAsync_LogsAFailure()
    {
        _h.Sheet(BudgetKind.Expenditure, 2026, H.Email, Meal);
        _h.Cells.FailWrites = new InvalidOperationException("db down");

        var (result, _) = await _h.Sut.RemoveLineAsync(H.Email, BudgetKind.Expenditure, 2026, "UTC", "ConsumerSpending", "MealOrEatOutExpenses", "KRW", TestContext.Current.CancellationToken);

        Assert.Equal(ServiceErrorKeys.TemporaryError, result.ErrorKey);
        Assert.Equal("Budget: failed to change the lines of Expenditure2026 of user@example.com", _h.Logger.LatestRecord.Message);
        _h.AssertTransactions(1, 0);
    }

    /// <summary>Carry-over fills the empty budget cells from last year's sheet and says how many.</summary>
    [Fact]
    public async Task CarryOverAsync_FillsFromLastYear()
    {
        long last = _h.Sheet(BudgetKind.Expenditure, 2025, H.Email, Meal);
        _h.Cell(last, Meal.Key, "B", "500");
        _h.Cell(last, Meal.Key, "C", "600");
        long now = _h.Sheet(BudgetKind.Expenditure, 2026, H.Email, Meal);
        _h.Cell(now, Meal.Key, "C", "9");

        var (result, sheet, copied) = await _h.Sut.CarryOverAsync(H.Email, BudgetKind.Expenditure, 2026, "UTC", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(1, copied);
        Assert.Equal(500m, sheet!.Cells.Single(c => c.Address == "B2").Value.Number);
        Assert.Equal(["500", "9"], _h.Cells.All.Where(c => c.SheetId == now).Select(c => c.Input).Order(StringComparer.Ordinal));
        _h.AssertTransactions(1, 1);
    }

    /// <summary>A failed carry-over is logged.</summary>
    [Fact]
    public async Task CarryOverAsync_LogsAFailure()
    {
        long last = _h.Sheet(BudgetKind.Expenditure, 2025, H.Email, Meal);
        _h.Cell(last, Meal.Key, "B", "500");
        _h.Sheet(BudgetKind.Expenditure, 2026, H.Email, Meal);
        _h.Cells.FailWrites = new InvalidOperationException("db down");

        var (result, _, _) = await _h.Sut.CarryOverAsync(H.Email, BudgetKind.Expenditure, 2026, "UTC", TestContext.Current.CancellationToken);

        Assert.Equal(ServiceErrorKeys.TemporaryError, result.ErrorKey);
        Assert.Equal("Budget: failed to carry over into Expenditure2026 of user@example.com", _h.Logger.LatestRecord.Message);
    }

    /// <summary>Every write refuses a bad year before touching anything.</summary>
    [Fact]
    public async Task Writes_RefuseABadYear()
    {
        Assert.Equal("Budget.YearInvalid", (await _h.Sut.EditAsync(H.Email, BudgetKind.Income, 0, "UTC", [], TestContext.Current.CancellationToken)).Result.ErrorCode);
        Assert.Equal("Budget.YearInvalid", (await _h.Sut.CopyAsync(H.Email, BudgetKind.Income, 0, "UTC", "B2", "C2", TestContext.Current.CancellationToken)).Result.ErrorCode);
        Assert.Equal("Budget.YearInvalid", (await _h.Sut.AddLineAsync(H.Email, BudgetKind.Income, 0, "UTC", "a", "b", "c", TestContext.Current.CancellationToken)).Result.ErrorCode);
        Assert.Equal("Budget.YearInvalid", (await _h.Sut.RemoveLineAsync(H.Email, BudgetKind.Income, 0, "UTC", "a", "b", "c", TestContext.Current.CancellationToken)).Result.ErrorCode);
        Assert.Equal("Budget.YearInvalid", (await _h.Sut.CarryOverAsync(H.Email, BudgetKind.Income, 0, "UTC", TestContext.Current.CancellationToken)).Result.ErrorCode);
        _h.AssertTransactions(0, 0);
    }
}
