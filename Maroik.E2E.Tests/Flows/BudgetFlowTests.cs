using Maroik.E2E.Tests.Fixtures;
using Microsoft.Playwright;

namespace Maroik.E2E.Tests.Flows;

/// <summary>
/// End-to-end tests for the budget sheet (<c>/AccountBook/IncomeBudget</c>) as a signed-in User, in a real browser against the real website:
/// the grid drawn with the default lines, typing a budget, a formula coloured while it is typed, fill right, the Remaining view against an
/// actual income record, and the Excel download. The account owns one KRW asset, so its sheet starts with one KRW line per income class:
/// 1 Regular income, 2 Labor, 3 Business, 4 Pension, 5 Financial, 6 Rental, 7 Other, 8 subtotal, 9 Irregular income, 10 Labor, 11 Other,
/// 12 subtotal, 13 total, 14.. free rows.
/// </summary>
[Collection("E2E")]
public class BudgetFlowTests(E2ESharedFixture fixture) : E2ETestBase(fixture)
{
    /// <summary>A signed-in User with a KRW asset, on the Income Budget page with the grid drawn.</summary>
    private async Task<(IPage Page, SeededAccount Account)> OpenAsync()
    {
        var (page, account) = await NewSignedInPageAsync();
        await Db.SeedAssetAsync(account.Email, "Wallet", 0m);
        await page.WaitForURLAsync(url => url.Contains("/Dashboard/", StringComparison.Ordinal), new PageWaitForURLOptions { Timeout = 30_000 });
        await GotoAsync(page, "/AccountBook/IncomeBudget");
        await Assertions.Expect(Cell(page, "A2")).ToContainTextAsync("근로수입");
        return (page, account);
    }

    /// <summary>The grid cell at an address.</summary>
    private static ILocator Cell(IPage page, string address) => page.Locator($"#budgetGrid td[data-address='{address}']");

    /// <summary>Selects a cell and types into it (typing starts the edit), then presses Enter.</summary>
    private static async Task EnterAsync(IPage page, string address, string input)
    {
        await Cell(page, address).ClickAsync();
        await page.Keyboard.TypeAsync(input);
        await page.Keyboard.PressAsync("Enter");
    }

    /// <summary>The sheet shows the default KRW lines with their Korean labels, the subtotal and total rows, and the free rows.</summary>
    [Fact]
    public async Task TheSheet_ShowsTheDefaultLines()
    {
        var (page, _) = await OpenAsync();

        await Assertions.Expect(Cell(page, "A2")).ToContainTextAsync("KRW");
        await Assertions.Expect(page.Locator("#budgetGrid tbody tr")).ToHaveCountAsync(13 + 30);
        await Assertions.Expect(page.Locator("#budgetGrid tbody tr").Nth(12)).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("budget-row-total"));
        await Assertions.Expect(page.Locator("#budgetNameBox")).ToHaveTextAsync("B2");
        await Assertions.Expect(page.Locator("#budgetNoLines")).ToBeHiddenAsync();
    }

    /// <summary>
    /// An account with no asset has no currency for the default lines: instead of a bare grid the page says how lines come about.
    /// </summary>
    [Fact]
    public async Task ASheetWithoutLines_SaysHowLinesComeAbout()
    {
        var (page, _) = await NewSignedInPageAsync();
        await page.WaitForURLAsync(url => url.Contains("/Dashboard/", StringComparison.Ordinal), new PageWaitForURLOptions { Timeout = 30_000 });
        await GotoAsync(page, "/AccountBook/IncomeBudget");

        await Assertions.Expect(page.Locator("#budgetNoLines")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#budgetNoLines")).ToContainTextAsync("통화 줄 추가");
        await Assertions.Expect(page.Locator("#budgetGrid tbody tr.budget-row-line")).ToHaveCountAsync(0);
    }

    /// <summary>A typed budget is saved; the year total, the subtotal and the total follow.</summary>
    [Fact]
    public async Task TypingABudget_SavesIt_AndTheSumsFollow()
    {
        var (page, account) = await OpenAsync();

        await EnterAsync(page, "B2", "3000000");

        await Assertions.Expect(Cell(page, "B2")).ToHaveTextAsync("3,000,000");
        await Assertions.Expect(Cell(page, "N2")).ToHaveTextAsync("3,000,000");
        await Assertions.Expect(Cell(page, "B8")).ToHaveTextAsync("3,000,000");
        await Assertions.Expect(Cell(page, "B13")).ToHaveTextAsync("3,000,000");
        await Assertions.Expect(page.Locator("#budgetNameBox")).ToHaveTextAsync("B3");
        Assert.Equal("3000000", await Db.ScalarAsync<string>(
            """SELECT c."Input" FROM "BudgetSheetCell" c JOIN "BudgetSheet" s ON s."Id" = c."SheetId" WHERE s."AccountEmail" = @email AND c."Column" = 'B'""", ("email", account.Email)));
    }

    /// <summary>A formula is coloured while it is typed (the reference and its outlined cell share a colour) and calculates when saved.</summary>
    [Fact]
    public async Task AFormula_IsColouredWhileTyped_AndCalculates()
    {
        var (page, _) = await OpenAsync();
        await EnterAsync(page, "B2", "100");

        await Cell(page, "C2").ClickAsync();
        await page.Keyboard.TypeAsync("=B2*2");

        await Assertions.Expect(page.Locator(".budget-formula-input .cm-budget-reference.cm-budget-ref-0")).ToHaveTextAsync("B2");
        await Assertions.Expect(Cell(page, "B2")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("budget-ref-0"));
        await Assertions.Expect(page.Locator("#budgetPreviewValue")).ToHaveTextAsync("200");
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(Cell(page, "C2")).ToHaveTextAsync("200");
        await Assertions.Expect(Cell(page, "B2")).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("budget-ref-0"));
    }

    /// <summary>A formula Excel would refuse lists its problems while typed and, when saved, is refused with the first one, in Korean.</summary>
    [Fact]
    public async Task ABrokenFormula_IsUnderlined_AndRefused()
    {
        var (page, _) = await OpenAsync();

        await Cell(page, "B2").ClickAsync();
        await page.Keyboard.TypeAsync("=ROUND(1");
        await Assertions.Expect(page.Locator("#budgetProblems li", new PageLocatorOptions { HasTextString = "')'" })).ToHaveCountAsync(1);
        await Assertions.Expect(page.Locator("#budgetProblems li", new PageLocatorOptions { HasTextString = "ROUND" })).ToHaveCountAsync(1);
        await page.Keyboard.PressAsync("Enter");

        // The first problem is the reason: ROUND takes two arguments. The cell stays open with what was typed.
        await Assertions.Expect(page.Locator(".toast-error")).ToContainTextAsync("ROUND");
        await Assertions.Expect(Cell(page, "B2")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("budget-editing"));
    }

    /// <summary>Ctrl+R fills a formula right with its references moved.</summary>
    [Fact]
    public async Task FillRight_MovesTheReferences()
    {
        var (page, _) = await OpenAsync();
        await EnterAsync(page, "B2", "1");
        await EnterAsync(page, "C2", "=B2*2");

        await Cell(page, "C2").ClickAsync();
        await Cell(page, "E2").ClickAsync(new LocatorClickOptions { Modifiers = [KeyboardModifier.Shift] });
        await page.Keyboard.PressAsync("Control+r");

        await Assertions.Expect(Cell(page, "E2")).ToHaveTextAsync("8");
        await Cell(page, "E2").ClickAsync();
        await Assertions.Expect(page.Locator(".budget-formula-input .CodeMirror")).ToContainTextAsync("=D2*2");
    }

    /// <summary>The Remaining view compares the month's budget with the month's recorded income (under the budget: a warning colour).</summary>
    [Fact]
    public async Task TheRemainingView_ComparesWithTheRecords()
    {
        var (page, account) = await OpenAsync();
        DateTime now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul"));
        string column = ((char)('A' + now.Month)).ToString();
        await Db.ExecuteAsync(
            """
            INSERT INTO "Income" ("AccountEmail", "MainClass", "SubClass", "Content", "Amount", "DepositMyAssetProductName", "Created", "Updated", "Note")
            VALUES (@email, 'RegularIncome', 'LaborIncome', 'Salary', 1000000, 'Wallet', now() AT TIME ZONE 'UTC', now() AT TIME ZONE 'UTC', '')
            """, ("email", account.Email));
        await EnterAsync(page, column + "2", "3000000");

        await page.Locator("#budgetViewSwitch [data-view='remaining']").ClickAsync();

        await Assertions.Expect(Cell(page, column + "2")).ToHaveTextAsync("2,000,000");
        await Assertions.Expect(Cell(page, column + "2")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("budget-state-warn"));
        await page.Locator("#budgetViewSwitch [data-view='actual']").ClickAsync();
        await Assertions.Expect(Cell(page, column + "2")).ToHaveTextAsync("1,000,000");
    }

    /// <summary>The Excel button downloads the sheet as an .xlsx file.</summary>
    [Fact]
    public async Task Excel_DownloadsTheSheet()
    {
        var (page, _) = await OpenAsync();

        var download = await page.RunAndWaitForDownloadAsync(() => page.Locator("#btnBudgetExcel").ClickAsync());

        Assert.StartsWith("Income", download.SuggestedFilename, StringComparison.Ordinal);
        Assert.EndsWith(".xlsx", download.SuggestedFilename, StringComparison.Ordinal);
    }
}
