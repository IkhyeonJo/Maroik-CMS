using Maroik.Core.Domain.Budgets;
using Maroik.Core.Domain.Tests.Fixtures;

namespace Maroik.Core.Domain.Tests.Budgets;

/// <summary>Unit tests for the persisted budget aggregates: <see cref="BudgetSheet"/>, <see cref="BudgetSheetLine"/> and <see cref="BudgetSheetCell"/>.</summary>
public class BudgetAggregateTests
{
    /// <summary>The fixed "current time".</summary>
    private static readonly DateTime Now = new(2026, 5, 24, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>A sheet keeps its owner, kind and year; its name is the formula name.</summary>
    [Fact]
    public void ASheet_IsCreated()
    {
        BudgetSheet sheet = BudgetSheet.Create("User@Example.com", BudgetKind.Expenditure, 2026, Now).Value;

        Assert.Equal(("user@example.com", BudgetKind.Expenditure, 2026, "Expenditure2026"), (sheet.AccountEmail.Value, sheet.Kind, sheet.Year, sheet.Name));
        Assert.Equal((Now, Now), (sheet.Created, sheet.Updated));
    }

    /// <summary>A year outside 1..9999, an unknown kind or a bad e-mail is refused.</summary>
    [Fact]
    public void ASheet_RefusesBadValues()
    {
        ErrorAssert.Validation(BudgetSheet.Create("user@example.com", BudgetKind.Income, 0, Now), "Budget.YearInvalid", "The year must be between {0} and {1}.", 1, 9999);
        ErrorAssert.Validation(BudgetSheet.Create("user@example.com", BudgetKind.Income, 10000, Now), "Budget.YearInvalid", "The year must be between {0} and {1}.", 1, 9999);
        ErrorAssert.Validation(BudgetSheet.Create("user@example.com", (BudgetKind)5, 2026, Now), "Budget.KindInvalid", "Unknown budget kind.");
        Assert.True(BudgetSheet.Create("", BudgetKind.Income, 2026, Now).IsError);
    }

    /// <summary>Touching a sheet stamps its update; reconstitution keeps stored values.</summary>
    [Fact]
    public void ASheet_IsTouched_AndReconstituted()
    {
        BudgetSheet sheet = BudgetSheet.Reconstitute(7, "user@example.com", "Income", 2025, Now.AddDays(-1), Now.AddDays(-1));

        sheet.Touch(Now);

        Assert.Equal((7L, BudgetKind.Income, 2025, Now), (sheet.Id, sheet.Kind, sheet.Year, sheet.Updated));
    }

    /// <summary>A line row belongs to a sheet and carries its line.</summary>
    [Fact]
    public void ALine_IsCreated_AndReconstituted()
    {
        BudgetLine line = BudgetLine.Create(BudgetKind.Expenditure, "ConsumerSpending", "PinMoney", "KRW").Value;

        BudgetSheetLine created = BudgetSheetLine.Create(3, line, Now);
        BudgetSheetLine stored = BudgetSheetLine.Reconstitute(9, 3, "ConsumerSpending", "PinMoney", "KRW", Now);

        Assert.Equal((0L, 3L, line, Now), (created.Id, created.SheetId, created.Line, created.Created));
        Assert.Equal((9L, line), (stored.Id, stored.Line));
    }

    /// <summary>A cell row keeps its input; changing it stamps the update.</summary>
    [Fact]
    public void ACell_IsCreated_AndChanged()
    {
        BudgetSheetCell cell = BudgetSheetCell.Create(3, "F.1", "B", "=1", Now);

        cell.ChangeInput("2", Now.AddHours(1));

        Assert.Equal((3L, "F.1", "B", "2", Now.AddHours(1)), (cell.SheetId, cell.RowKey, cell.Column, cell.Input, cell.Updated));
        Assert.Equal("x", BudgetSheetCell.Reconstitute(1, 3, "F.2", "C", "x", Now).Input);
    }
}
