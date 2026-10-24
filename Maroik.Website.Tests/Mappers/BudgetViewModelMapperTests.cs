using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Budgets;
using Maroik.Website.Mappers;
using Maroik.Website.Models.ViewModels.AccountBooks;

namespace Maroik.Website.Tests.Mappers;

/// <summary>
/// Unit tests for <see cref="BudgetViewModelMapper"/>: the budget DTOs turned into what the page draws — localized row labels, values in
/// the amount format, each budget cell's colour by kind and state, localized formula problems — and the page's own model.
/// </summary>
public class BudgetViewModelMapperTests
{
    /// <summary>A localizer that marks what it translated.</summary>
    private static string L(string key) => "L:" + key;

    /// <summary>A number value.</summary>
    private static BudgetValueResponse Number(decimal n) => new() { Kind = "Number", Number = n };

    /// <summary>Rows get localized labels: the main class for a header, the sub-class for a line, Subtotal / Total, nothing for a free row.</summary>
    [Fact]
    public void Rows_GetLocalizedLabels()
    {
        var sheet = new BudgetSheetResponse
        {
            Kind = "Expenditure", Year = 2026, Exists = true,
            Rows =
            [
                new BudgetRowResponse { Number = 1, Kind = "Header", MainClass = "ConsumerSpending" },
                new BudgetRowResponse { Number = 2, Kind = "Line", MainClass = "ConsumerSpending", SubClass = "PinMoney", Currency = "KRW", EditableColumns = "BC" },
                new BudgetRowResponse { Number = 3, Kind = "Subtotal", MainClass = "ConsumerSpending", Currency = "KRW" },
                new BudgetRowResponse { Number = 4, Kind = "Total", Currency = "KRW" },
                new BudgetRowResponse { Number = 5, Kind = "Free" },
            ],
        };

        BudgetSheetViewModel vm = sheet.ToViewModel(L);

        Assert.Equal(("Expenditure", 2026, true, true), (vm.Kind, vm.Year, vm.Exists, vm.HasLines));
        Assert.Equal(["L:ConsumerSpending", "L:PinMoney", "L:Subtotal", "L:Total", ""], vm.Rows.Select(r => r.Label));
        Assert.Equal(("ConsumerSpending", "PinMoney", "KRW", "BC"), (vm.Rows[1].MainClass, vm.Rows[1].SubClass, vm.Rows[1].Currency, vm.Rows[1].Editable));
        Assert.Equal(("", "", ""), (vm.Rows[4].MainClass, vm.Rows[4].SubClass, vm.Rows[4].Currency));
    }

    /// <summary>
    /// A sheet without a single line (a new account with no asset, so no currency for the default lines) says so, so the page can tell
    /// the user how lines come about instead of showing an empty grid.
    /// </summary>
    [Fact]
    public void ASheetWithoutLines_SaysSo()
    {
        var sheet = new BudgetSheetResponse { Kind = "Income", Year = 2026, Rows = [new BudgetRowResponse { Number = 1, Kind = "Free" }] };

        Assert.False(sheet.ToViewModel(L).HasLines);
    }

    /// <summary>Values are shown in the amount format (every stored decimal, no trailing zeros); text, booleans and errors as their text.</summary>
    [Fact]
    public void Values_AreShownAsTheGridsShowAmounts()
    {
        Assert.Equal("1,234.5", BudgetViewModelMapper.Display(Number(1234.5m)));
        Assert.Equal("0.3333", BudgetViewModelMapper.Display(Number(1m / 3)));
        Assert.Equal("note", BudgetViewModelMapper.Display(new BudgetValueResponse { Kind = "Text", Text = "note" }));
        Assert.Equal("#REF!", BudgetViewModelMapper.Display(new BudgetValueResponse { Kind = "Error", Text = "#REF!" }));
        Assert.Equal("", BudgetViewModelMapper.Display(new BudgetValueResponse { Kind = "Empty" }));
    }

    /// <summary>A budget cell carries its actual and remaining amounts and its colour: for expenditure over is bad, for income under is a warning.</summary>
    [Theory]
    [InlineData("Expenditure", "Under", "good")]
    [InlineData("Expenditure", "Reached", "warn")]
    [InlineData("Expenditure", "Over", "bad")]
    [InlineData("Income", "Under", "warn")]
    [InlineData("Income", "Reached", "good")]
    [InlineData("Income", "Over", "good")]
    [InlineData("Income", "Unbudgeted", "unbudgeted")]
    [InlineData("Expenditure", "Unbudgeted", "unbudgeted")]
    [InlineData("Expenditure", "None", "")]
    public void BudgetCells_AreColouredByKindAndState(string kind, string state, string colour)
    {
        var sheet = new BudgetSheetResponse
        {
            Kind = kind,
            Cells = [new BudgetCellResponse { Address = "B2", Role = "Budget", Input = "=1", Value = Number(100), Budget = 100, Actual = 120, Remaining = -20, State = state }],
        };

        BudgetCellViewModel cell = sheet.ToViewModel(L).Cells.Single();

        Assert.Equal(("B2", "Budget", "=1", "100", "Number", "120", "-20", colour), (cell.Address, cell.Role, cell.Input, cell.Display, cell.ValueKind, cell.Actual, cell.Remaining, cell.State));
    }

    /// <summary>Other cells have no actual, remaining or colour; a budget cell without a budget has no remaining.</summary>
    [Fact]
    public void OtherCells_HaveNoStatus()
    {
        var sheet = new BudgetSheetResponse
        {
            Kind = "Income",
            Cells =
            [
                new BudgetCellResponse { Address = "N2", Role = "Computed", Input = "=SUM(B2:M2)", Value = Number(0) },
                new BudgetCellResponse { Address = "C2", Role = "Budget", Value = new BudgetValueResponse { Kind = "Empty" }, Actual = 5, State = "Unbudgeted" },
            ],
        };

        var cells = sheet.ToViewModel(L).Cells;

        Assert.Equal(("", "", ""), (cells[0].Actual, cells[0].Remaining, cells[0].State));
        Assert.Equal(("", "5", ""), (cells[1].Input, cells[1].Actual, cells[1].Remaining));
    }

    /// <summary>Unbudgeted records get the localized sub-class and their months in the amount format.</summary>
    [Fact]
    public void Unbudgeted_AreShown()
    {
        var sheet = new BudgetSheetResponse
        {
            Kind = "Expenditure",
            Unbudgeted = [new BudgetUnbudgetedResponse { MainClass = "ConsumerSpending", SubClass = "PinMoney", Currency = "USD", Months = [.. Enumerable.Repeat(0m, 11), 1500m] }],
        };

        BudgetUnbudgetedViewModel line = sheet.ToViewModel(L).Unbudgeted.Single();

        Assert.Equal(("L:PinMoney", "ConsumerSpending", "PinMoney", "USD", "1,500", "0"), (line.Label, line.MainClass, line.SubClass, line.Currency, line.Months[11], line.Months[0]));
    }

    /// <summary>A preview's problems are localized with their arguments; tokens and references keep their positions; absent indexes are -1.</summary>
    [Fact]
    public void APreview_IsLocalized()
    {
        var preview = new BudgetPreviewResponse
        {
            Tokens = [new BudgetTokenResponse { Start = 0, Length = 1, Class = "operator" }, new BudgetTokenResponse { Start = 1, Length = 2, Class = "reference", Reference = 0 }],
            References = [new BudgetReferenceResponse { Index = 0, TopLeft = "B2", BottomRight = "C3" }, new BudgetReferenceResponse { Index = 1 }],
            Problems = [new BudgetProblemResponse { Start = 3, Length = 0, ErrorKey = "There is no function named {0}.", ErrorArgs = ["FOO"], Blocking = false }],
            CanSave = true,
            Value = Number(12),
        };

        BudgetPreviewViewModel vm = preview.ToViewModel((key, args) => key + "|" + string.Join(",", args));

        Assert.Equal([(-1, -1), (0, -1)], vm.Tokens.Select(t => (t.Reference, t.Partner)));
        Assert.Equal((1, 2, "reference"), (vm.Tokens[1].Start, vm.Tokens[1].Length, vm.Tokens[1].Class));
        Assert.Equal([("B2", "C3"), ("", "")], vm.References.Select(r => (r.TopLeft, r.BottomRight)));
        Assert.Equal((3, 0, "There is no function named {0}.|FOO", false), (vm.Problems[0].Start, vm.Problems[0].Length, vm.Problems[0].Message, vm.Problems[0].Blocking));
        Assert.True(vm.CanSave);
        Assert.Equal("12", vm.Value);
        Assert.Equal("", new BudgetPreviewResponse().ToViewModel((k, _) => k).Value);
    }

    /// <summary>The page opens on the account's current year, offers the kind's classes (with their localized names) and the distinct currencies of its assets.</summary>
    [Fact]
    public void ThePage_OpensOnTheCurrentYear()
    {
        AssetResponse[] assets = [new() { ProductName = "A", MonetaryUnit = "KRW" }, new() { ProductName = "B", MonetaryUnit = "USD" }, new() { ProductName = "C", MonetaryUnit = "KRW" }, new() { ProductName = "D" }];

        BudgetPageViewModel page = BudgetViewModelMapper.Page(BudgetKind.Income, assets, new DateTime(2026, 12, 31, 16, 0, 0, DateTimeKind.Utc), "Asia/Seoul", L);

        Assert.Equal(("Income", 2027), (page.Kind, page.Year));
        Assert.Equal(["RegularIncome", "IrregularIncome"], page.MainClasses);
        Assert.Equal(["LaborIncome", "OtherIncome"], page.SubClassesByMainClass["IrregularIncome"]);
        Assert.Equal(["KRW", "USD"], page.Currencies);
        Assert.Equal(8, page.ClassLabels.Count);
        Assert.Equal("L:IrregularIncome", page.ClassLabels["IrregularIncome"]);
        Assert.Equal("L:RentalIncome", page.ClassLabels["RentalIncome"]);
        Assert.Equal(["RegularSavings", "NonConsumerSpending", "ConsumerSpending"], BudgetViewModelMapper.Page(BudgetKind.Expenditure, [], DateTime.UnixEpoch, "UTC", L).MainClasses);
    }

    /// <summary>
    /// The column headers say what the columns hold: B..M the months (January..December) and N the year total, localized; the
    /// label column A and the free columns O..Z have no caption.
    /// </summary>
    [Fact]
    public void ThePage_CaptionsTheMonthAndYearTotalColumns()
    {
        BudgetPageViewModel page = BudgetViewModelMapper.Page(BudgetKind.Income, [], DateTime.UnixEpoch, "UTC", L);

        Assert.Equal(new Dictionary<string, string>
        {
            ["B"] = "L:January", ["C"] = "L:February", ["D"] = "L:March", ["E"] = "L:April", ["F"] = "L:May", ["G"] = "L:June",
            ["H"] = "L:July", ["I"] = "L:August", ["J"] = "L:September", ["K"] = "L:October", ["L"] = "L:November", ["M"] = "L:December",
            ["N"] = "L:Year total",
        }, page.ColumnCaptions);
    }
}
