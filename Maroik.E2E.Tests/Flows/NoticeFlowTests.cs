using Maroik.E2E.Tests.Infrastructure;
using Microsoft.Playwright;

namespace Maroik.E2E.Tests.Flows;

/// <summary>
/// End-to-end tests for the Notice pages (fixed income / fixed expenditure schedules) as a signed-in User.
/// Includes the regression flows for the edit form's "Unpunctuality" checkbox, which used to reach the
/// server and then be silently dropped, so the flag could never be switched on or off.
/// </summary>
[Collection("E2E")]
public class NoticeFlowTests(E2ESharedFixture fixture) : E2ETestBase(fixture)
{
    /// <summary>Selects the grid row containing <paramref name="rowText"/> and waits until it is highlighted.</summary>
    private static async Task SelectGridRowAsync(IPage page, string rowText)
    {
        // The grid loads over AJAX; a click on a row's cell selects it (the row gets "table-primary").
        var row = page.Locator(".mvc-grid tbody tr", new PageLocatorOptions { HasTextString = rowText });
        await Assertions.Expect(row).ToBeVisibleAsync();
        // The first column is the hidden ID; click the first visible cell.
        await row.Locator("td:not(.mvc-grid-hidden)").First.ClickAsync();
        await Assertions.Expect(row).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("table-primary"));
    }

    // -- Fixed income ---------------------------------------------------------------

    /// <summary>The fixed-income page lists the signed-in User's schedules and nobody else's.</summary>
    [Fact]
    public async Task FixedIncome_Grid_ShowsOnlyTheSignedInUsersSchedules()
    {
        var (page, account) = await NewSignedInPageAsync();
        SeededAccount other = await Db.SeedAccountAsync();
        await Db.SeedAssetAsync(account.Email, "SavingsAccount", 0);
        await Db.SeedAssetAsync(other.Email, "OthersAccount", 0);
        await Db.SeedFixedIncomeAsync(account.Email, "SavingsAccount", "My monthly salary");
        await Db.SeedFixedIncomeAsync(other.Email, "OthersAccount", "Someone elses salary");

        await GotoAsync(page, "/Notice/FixedIncome");

        await Assertions.Expect(page.Locator(".mvc-grid")).ToContainTextAsync("My monthly salary");
        await Assertions.Expect(page.Locator(".mvc-grid")).Not.ToContainTextAsync("Someone elses salary");
    }

    /// <summary>Ticking "Unpunctuality" on a fixed income and saving persists the flag; unticking clears it again.</summary>
    [Fact]
    public async Task FixedIncome_Edit_PersistsTheUnpunctualityFlag_BothWays()
    {
        var (page, account) = await NewSignedInPageAsync();
        await Db.SeedAssetAsync(account.Email, "SavingsAccount", 0);
        await Db.SeedFixedIncomeAsync(account.Email, "SavingsAccount", "Quarterly bonus");
        const string flagSql = """SELECT "Unpunctuality" FROM "FixedIncome" WHERE "AccountEmail" = @email AND "Content" = 'Quarterly bonus'""";
        Assert.False(await Db.ScalarAsync<bool>(flagSql, ("email", account.Email)));

        // -- switch it on
        await GotoAsync(page, "/Notice/FixedIncome");
        await SelectGridRowAsync(page, "Quarterly bonus");
        await page.Locator("#btnEditFixedIncomeGridRow").ClickAsync();
        await Assertions.Expect(page.Locator("#editFixedIncomeDialogModal")).ToBeVisibleAsync();
        await page.Locator("#editFixedIncomeUnpunctuality").CheckAsync();
        await page.Locator("#formEditFixedIncome input[type='submit']").ClickAsync();
        await Assertions.Expect(page.Locator(".toast-success")).ToBeVisibleAsync();

        Assert.True(await Db.ScalarAsync<bool>(flagSql, ("email", account.Email)));

        // -- and off again
        await GotoAsync(page, "/Notice/FixedIncome");
        await SelectGridRowAsync(page, "Quarterly bonus");
        await page.Locator("#btnEditFixedIncomeGridRow").ClickAsync();
        await Assertions.Expect(page.Locator("#editFixedIncomeDialogModal")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#editFixedIncomeUnpunctuality")).ToBeCheckedAsync();
        await page.Locator("#editFixedIncomeUnpunctuality").UncheckAsync();
        await page.Locator("#formEditFixedIncome input[type='submit']").ClickAsync();
        await Assertions.Expect(page.Locator(".toast-success")).ToBeVisibleAsync();

        Assert.False(await Db.ScalarAsync<bool>(flagSql, ("email", account.Email)));
    }

    // -- Fixed expenditure ------------------------------------------------------------

    /// <summary>The same Unpunctuality round trip for a fixed expenditure.</summary>
    [Fact]
    public async Task FixedExpenditure_Edit_PersistsTheUnpunctualityFlag()
    {
        var (page, account) = await NewSignedInPageAsync();
        await Db.SeedAssetAsync(account.Email, "Cash-Wallet", 5000);
        await Db.SeedFixedExpenditureAsync(account.Email, "Cash-Wallet", "Gym membership");
        const string flagSql = """SELECT "Unpunctuality" FROM "FixedExpenditure" WHERE "AccountEmail" = @email AND "Content" = 'Gym membership'""";
        Assert.False(await Db.ScalarAsync<bool>(flagSql, ("email", account.Email)));

        await GotoAsync(page, "/Notice/FixedExpenditure");
        await SelectGridRowAsync(page, "Gym membership");
        await page.Locator("#btnEditFixedExpenditureGridRow").ClickAsync();
        await Assertions.Expect(page.Locator("#editFixedExpenditureDialogModal")).ToBeVisibleAsync();
        await page.Locator("#editFixedExpenditureUnpunctuality").CheckAsync();
        await page.Locator("#formEditFixedExpenditure input[type='submit']").ClickAsync();
        await Assertions.Expect(page.Locator(".toast-success")).ToBeVisibleAsync();

        Assert.True(await Db.ScalarAsync<bool>(flagSql, ("email", account.Email)));
    }

    /// <summary>The fixed-expenditure page lists only the signed-in User's schedules.</summary>
    [Fact]
    public async Task FixedExpenditure_Grid_ShowsOnlyTheSignedInUsersSchedules()
    {
        var (page, account) = await NewSignedInPageAsync();
        SeededAccount other = await Db.SeedAccountAsync();
        await Db.SeedAssetAsync(account.Email, "Cash-Wallet", 100);
        await Db.SeedAssetAsync(other.Email, "Others-Wallet", 100);
        await Db.SeedFixedExpenditureAsync(account.Email, "Cash-Wallet", "My rent");
        await Db.SeedFixedExpenditureAsync(other.Email, "Others-Wallet", "Someone elses rent");

        await GotoAsync(page, "/Notice/FixedExpenditure");

        await Assertions.Expect(page.Locator(".mvc-grid")).ToContainTextAsync("My rent");
        await Assertions.Expect(page.Locator(".mvc-grid")).Not.ToContainTextAsync("Someone elses rent");
    }
}
