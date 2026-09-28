using Maroik.E2E.Tests.Infrastructure;
using Microsoft.Playwright;

namespace Maroik.E2E.Tests.Flows;

/// <summary>
/// End-to-end tests for the account book (assets, income, expenditure) as a signed-in User.
/// Each flow drives the real page — opens the modal, fills the form, saves — and then reads the
/// database back, so it proves the server persisted the row and moved the asset balance, not just
/// that the page said so.
/// </summary>
[Collection("E2E")]
public class AccountBookFlowTests(E2ESharedFixture fixture) : E2ETestBase(fixture)
{
    private static ILocator SuccessToast(IPage page) => page.Locator(".toast-success");

    private static ILocator ErrorToast(IPage page) => page.Locator(".toast-error");

    // -- Asset ----------------------------------------------------------------------

    /// <summary>A signed-in User can add an asset: it is persisted with the entered balance and shows in the grid.</summary>
    [Fact]
    public async Task Asset_Create_PersistsTheAsset_AndShowsItInTheGrid()
    {
        var (page, account) = await NewSignedInPageAsync();
        string name = $"Wallet-{Guid.NewGuid():N}"[..14];

        await GotoAsync(page, "/AccountBook/Asset");
        await page.Locator("button[data-target='#createAssetDialogModal']").ClickAsync();
        await page.Locator("#createAssetProductName").FillAsync(name);
        await page.Locator("#createAssetAmount").FillAsync("1000");
        await page.Locator("#createAssetMonetaryUnit").FillAsync("KRW");
        await page.Locator("#formCreateAsset input[type='submit']").ClickAsync();

        await Assertions.Expect(SuccessToast(page)).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".mvc-grid")).ToContainTextAsync(name);

        decimal amount = await Db.ScalarAsync<decimal>(
            """SELECT "Amount" FROM "Asset" WHERE "AccountEmail" = @email AND "ProductName" = @name""",
            ("email", account.Email), ("name", name));
        Assert.Equal(1000m, amount);
    }

    /// <summary>
    /// Regression test: an over-long product name used to reach the database and blow up as an
    /// unhandled 500. It is now refused up front with an error message, and nothing is stored.
    /// </summary>
    [Fact]
    public async Task Asset_Create_WithAnOverlongName_IsRefusedWithAnErrorMessage()
    {
        var (page, account) = await NewSignedInPageAsync();

        await GotoAsync(page, "/AccountBook/Asset");
        await page.Locator("button[data-target='#createAssetDialogModal']").ClickAsync();
        await page.Locator("#createAssetProductName").FillAsync(new string('a', 300));
        await page.Locator("#createAssetAmount").FillAsync("1000");
        await page.Locator("#createAssetMonetaryUnit").FillAsync("KRW");
        await page.Locator("#formCreateAsset input[type='submit']").ClickAsync();

        // The server's own validation message (not a generic failure): the 255-character limit. The
        // browser's locale decides the language, so only the number is asserted.
        await Assertions.Expect(ErrorToast(page)).ToContainTextAsync("255");

        long count = await Db.ScalarAsync<long>("""SELECT count(*) FROM "Asset" WHERE "AccountEmail" = @email""", ("email", account.Email));
        Assert.Equal(0, count);
    }

    /// <summary>An amount beyond what the database column holds is refused the same way.</summary>
    [Fact]
    public async Task Asset_Create_WithAnOutOfRangeAmount_IsRefusedWithAnErrorMessage()
    {
        var (page, account) = await NewSignedInPageAsync();

        await GotoAsync(page, "/AccountBook/Asset");
        await page.Locator("button[data-target='#createAssetDialogModal']").ClickAsync();
        await page.Locator("#createAssetProductName").FillAsync("Huge");
        await page.Locator("#createAssetAmount").FillAsync("99999999999999999");
        await page.Locator("#createAssetMonetaryUnit").FillAsync("KRW");
        await page.Locator("#formCreateAsset input[type='submit']").ClickAsync();

        await Assertions.Expect(ErrorToast(page)).ToContainTextAsync("9,999,999,999,999,999.99");

        long count = await Db.ScalarAsync<long>("""SELECT count(*) FROM "Asset" WHERE "AccountEmail" = @email""", ("email", account.Email));
        Assert.Equal(0, count);
    }

    /// <summary>A User only ever sees their own assets in the grid.</summary>
    [Fact]
    public async Task Asset_Grid_ShowsOnlyTheSignedInUsersAssets()
    {
        var (page, account) = await NewSignedInPageAsync();
        SeededAccount other = await Db.SeedAccountAsync();
        await Db.SeedAssetAsync(account.Email, "MyOwnAsset", 10);
        await Db.SeedAssetAsync(other.Email, "SomeoneElsesAsset", 10);

        await GotoAsync(page, "/AccountBook/Asset");

        await Assertions.Expect(page.Locator(".mvc-grid")).ToContainTextAsync("MyOwnAsset");
        await Assertions.Expect(page.Locator(".mvc-grid")).Not.ToContainTextAsync("SomeoneElsesAsset");
    }

    // -- Income ---------------------------------------------------------------------

    /// <summary>Recording an income stores it and adds the amount to the deposit asset's balance.</summary>
    /// <summary>
    /// The edit lookup sends the product name in the query string; a name with '&amp;' and '#' must arrive intact
    /// (it used to be concatenated unencoded, so such an asset could not be opened for editing).
    /// </summary>
    [Fact]
    public async Task Asset_Edit_OpensAnAssetWhoseNameNeedsUrlEncoding()
    {
        var (page, account) = await NewSignedInPageAsync();
        const string name = "A&B Bank #1";
        await Db.SeedAssetAsync(account.Email, name, 10);

        await GotoAsync(page, "/AccountBook/Asset");
        var row = page.Locator(".mvc-grid tbody tr", new PageLocatorOptions { HasTextString = name });
        await Assertions.Expect(row).ToBeVisibleAsync();
        await row.Locator("td:not(.mvc-grid-hidden)").First.ClickAsync();
        await page.Locator("#btnEditAssetGridRow").ClickAsync();

        await Assertions.Expect(page.Locator("#editAssetDialogModal")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#editAssetProductName")).ToHaveValueAsync(name);
    }

    /// <summary>Verifies that creating an income through the page persists it and adds its amount to the deposit asset's balance.</summary>
    [Fact]
    public async Task Income_Create_AddsTheAmountToTheDepositAsset()
    {
        var (page, account) = await NewSignedInPageAsync();
        await Db.SeedAssetAsync(account.Email, "Salary-Account", 1000);

        await GotoAsync(page, "/AccountBook/Income");
        await page.Locator("button[data-target='#createIncomeDialogModal']").ClickAsync();
        await page.Locator("#createIncomeContent").FillAsync("March salary");
        await page.Locator("#createIncomeAmount").FillAsync("250");
        await page.Locator("#createIncomeDepositMyAssetProductName").SelectOptionAsync("Salary-Account");
        await page.Locator("#formCreateIncome input[type='submit']").ClickAsync();

        await Assertions.Expect(SuccessToast(page)).ToBeVisibleAsync();

        long incomes = await Db.ScalarAsync<long>(
            """SELECT count(*) FROM "Income" WHERE "AccountEmail" = @email AND "Content" = 'March salary' AND "Amount" = 250""",
            ("email", account.Email));
        decimal balance = await Db.ScalarAsync<decimal>(
            """SELECT "Amount" FROM "Asset" WHERE "AccountEmail" = @email AND "ProductName" = 'Salary-Account'""",
            ("email", account.Email));
        Assert.Equal(1, incomes);
        Assert.Equal(1250m, balance);
    }

    /// <summary>An income whose amount is beyond the database range is refused and leaves the balance untouched.</summary>
    [Fact]
    public async Task Income_Create_WithAnOutOfRangeAmount_IsRefused_AndLeavesTheBalanceUntouched()
    {
        var (page, account) = await NewSignedInPageAsync();
        await Db.SeedAssetAsync(account.Email, "Salary-Account", 1000);

        await GotoAsync(page, "/AccountBook/Income");
        await page.Locator("button[data-target='#createIncomeDialogModal']").ClickAsync();
        await page.Locator("#createIncomeContent").FillAsync("Too much");
        await page.Locator("#createIncomeAmount").FillAsync("99999999999999999");
        await page.Locator("#createIncomeDepositMyAssetProductName").SelectOptionAsync("Salary-Account");
        await page.Locator("#formCreateIncome input[type='submit']").ClickAsync();

        await Assertions.Expect(ErrorToast(page)).ToContainTextAsync("9,999,999,999,999,999.99");

        long incomes = await Db.ScalarAsync<long>("""SELECT count(*) FROM "Income" WHERE "AccountEmail" = @email""", ("email", account.Email));
        decimal balance = await Db.ScalarAsync<decimal>(
            """SELECT "Amount" FROM "Asset" WHERE "AccountEmail" = @email AND "ProductName" = 'Salary-Account'""", ("email", account.Email));
        Assert.Equal(0, incomes);
        Assert.Equal(1000m, balance);
    }

    // -- Expenditure ----------------------------------------------------------------

    /// <summary>Recording an expenditure stores it and takes the amount off the payment asset's balance.</summary>
    [Fact]
    public async Task Expenditure_Create_DeductsTheAmountFromThePaymentAsset()
    {
        var (page, account) = await NewSignedInPageAsync();
        await Db.SeedAssetAsync(account.Email, "Cash-Wallet", 5000);

        await GotoAsync(page, "/AccountBook/Expenditure");
        await page.Locator("button[data-target='#createExpenditureDialogModal']").ClickAsync();
        await page.Locator("#createExpenditureMainClass").SelectOptionAsync("ConsumerSpending");
        await page.Locator("#createExpenditureSubClass").SelectOptionAsync("MealOrEatOutExpenses");
        await page.Locator("#createExpenditureContent").FillAsync("Lunch");
        await page.Locator("#createExpenditureAmount").FillAsync("1200");
        await page.Locator("#createExpenditurePaymentMethod").SelectOptionAsync("Cash-Wallet");
        await page.Locator("#formCreateExpenditure input[type='submit']").ClickAsync();

        await Assertions.Expect(SuccessToast(page)).ToBeVisibleAsync();

        long rows = await Db.ScalarAsync<long>(
            """SELECT count(*) FROM "Expenditure" WHERE "AccountEmail" = @email AND "Content" = 'Lunch' AND "Amount" = 1200""",
            ("email", account.Email));
        decimal balance = await Db.ScalarAsync<decimal>(
            """SELECT "Amount" FROM "Asset" WHERE "AccountEmail" = @email AND "ProductName" = 'Cash-Wallet'""", ("email", account.Email));
        Assert.Equal(1, rows);
        Assert.Equal(3800m, balance);
    }
}
