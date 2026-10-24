using System.Data;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Accounts;
using Maroik.Core.Domain.Budgets;
using Maroik.Core.Domain.Finances;
using Maroik.Core.Service.Services;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Maroik.Core.Service.Tests.Fixtures;

/// <summary>
/// Builds a <see cref="BudgetService"/> for its tests: in-memory sheet / line / cell repositories, a mocked account (time zone UTC) with
/// one KRW asset (so new sheets get KRW lines), income and expenditure records the tests add, a mocked unit of work, a captured log and a
/// stopped clock.
/// </summary>
internal sealed class BudgetServiceHarness
{
    /// <summary>The account the tests act as.</summary>
    public const string Email = "user@example.com";

    /// <summary>The fixed "current time".</summary>
    public static readonly DateTime Now = new(2026, 5, 24, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>The lines.</summary>
    public InMemoryBudgetSheetLineRepository Lines { get; } = new();

    /// <summary>The cells.</summary>
    public InMemoryBudgetSheetCellRepository Cells { get; } = new();

    /// <summary>The sheets.</summary>
    public InMemoryBudgetSheetRepository Sheets { get; }

    /// <summary>The account repository.</summary>
    public Mock<IAccountRepository> Accounts { get; } = new();

    /// <summary>The asset repository.</summary>
    public Mock<IAssetRepository> Assets { get; } = new();

    /// <summary>The income repository.</summary>
    public Mock<IIncomeRepository> Incomes { get; } = new();

    /// <summary>The expenditure repository.</summary>
    public Mock<IExpenditureRepository> Expenditures { get; } = new();

    /// <summary>The unit of work.</summary>
    public Mock<IUnitOfWork> UnitOfWork { get; } = new();

    /// <summary>The captured log.</summary>
    public FakeLogger<BudgetService> Logger { get; } = new();

    /// <summary>The stopped clock.</summary>
    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(Now));

    /// <summary>The expenditure records the repository answers with.</summary>
    public List<Expenditure> Spent { get; } = [];

    /// <summary>Wires the mocks.</summary>
    public BudgetServiceHarness()
    {
        Sheets = new InMemoryBudgetSheetRepository(Lines);
        Account account = Domain.Accounts.Account.Reconstitute(Email, "hash", "User", null, "User", "UTC", "KRW", false, 0, true, true, null, null, Now, Now, null, false, "stamp", false);
        Accounts.Setup(a => a.FindByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(account);
        Accounts.Setup(a => a.FindByEmailForUpdateAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(account);
        Assets.Setup(a => a.GetByAccountEmailAsync(Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Asset.Reconstitute("Wallet", Email, "FreeDepositAndWithdrawal", 0m, "KRW", null, false, Now, Now)]);
        Incomes.Setup(i => i.GetByAccountEmailAndDateRangeAsync(Email, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        Expenditures.Setup(e => e.GetByAccountEmailAndDateRangeAsync(Email, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, DateTime from, DateTime to, CancellationToken _) => [.. Spent.Where(x => x.Created >= from && x.Created < to)]);
        UnitOfWork.Setup(u => u.BeginAsync(It.IsAny<IsolationLevel?>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        UnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        UnitOfWork.Setup(u => u.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    /// <summary>The service under test.</summary>
    public BudgetService Sut => new(Sheets, Lines, Cells, Accounts.Object, Assets.Object, Incomes.Object, Expenditures.Object, UnitOfWork.Object, Logger, Time);

    /// <summary>Stores a sheet with lines for <see cref="Email"/> (or another account) and returns its id.</summary>
    public long Sheet(BudgetKind kind, int year, string email = Email, params BudgetLine[] lines)
    {
        _ = Sheets.CreateWithLinesAsync(BudgetSheet.Create(email, kind, year, Now.AddDays(-1)).Value, lines);
        return Sheets.All.Max(s => s.Id);
    }

    /// <summary>Stores a cell input of a sheet.</summary>
    public void Cell(long sheetId, string rowKey, string column, string input) =>
        _ = Cells.CreateAsync(BudgetSheetCell.Create(sheetId, rowKey, column, input, Now.AddDays(-1)));

    /// <summary>Adds an expenditure record created at <paramref name="createdUtc"/>.</summary>
    public void Spend(BudgetLine line, decimal amount, DateTime createdUtc) =>
        Spent.Add(Expenditure.Reconstitute(Spent.Count + 1, Email, line.MainClass, line.SubClass, "c", amount, line.Currency, "card", null, null, createdUtc, createdUtc));

    /// <summary>Asserts the unit of work ran <paramref name="begun"/> transactions, committing <paramref name="committed"/> and rolling back the rest.</summary>
    public void AssertTransactions(int begun, int committed)
    {
        UnitOfWork.Verify(u => u.BeginAsync(It.IsAny<IsolationLevel?>(), It.IsAny<CancellationToken>()), Times.Exactly(begun));
        UnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Exactly(committed));
        UnitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Exactly(begun - committed));
    }
}
