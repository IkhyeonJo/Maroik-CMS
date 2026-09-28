using Maroik.E2E.Tests.Infrastructure;
using Microsoft.Playwright;

namespace Maroik.E2E.Tests.Flows;

/// <summary>
/// End-to-end tests for real sign-in and for role-based access: which pages a signed-in User or
/// Admin — and an anonymous visitor — can actually open in a browser. Accounts are seeded straight
/// into the database and signed in through the real login form.
/// </summary>
[Collection("E2E")]
public class SignedInFlowTests(E2ESharedFixture fixture) : E2ETestBase(fixture)
{
    // -- Sign in ----------------------------------------------------------------

    /// <summary>A User who signs in lands on the user dashboard.</summary>
    [Fact]
    public async Task Login_AsUser_LandsOnTheUserDashboard()
    {
        var (page, _) = await NewSignedInPageAsync();

        await page.WaitForURLAsync(url => url.Contains("/Dashboard/UserIndex", StringComparison.Ordinal),
            new PageWaitForURLOptions { Timeout = 30_000 });
        Assert.Contains("/Dashboard/UserIndex", page.Url, StringComparison.Ordinal);
    }

    /// <summary>An Admin who signs in lands on the admin dashboard.</summary>
    [Fact]
    public async Task Login_AsAdmin_LandsOnTheAdminDashboard()
    {
        var (page, _) = await NewSignedInPageAsync("Admin");

        await page.WaitForURLAsync(url => url.Contains("/Dashboard/AdminIndex", StringComparison.Ordinal),
            new PageWaitForURLOptions { Timeout = 30_000 });
        Assert.Contains("/Dashboard/AdminIndex", page.Url, StringComparison.Ordinal);
    }

    /// <summary>A wrong password is refused: the browser stays on the login page and no session is issued.</summary>
    [Fact]
    public async Task Login_WithWrongPassword_StaysOnTheLoginPage()
    {
        SeededAccount account = await Db.SeedAccountAsync();
        var page = await NewPageAsync();

        await GotoAsync(page, "/Account/Login");
        await page.Locator("input[name='Email']").FillAsync(account.Email);
        await page.Locator("#Password").FillAsync("Wr0ng-Password!");
        await page.Locator("#btnSignIn").ClickAsync();

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        Assert.Contains("/Account/Login", page.Url, StringComparison.Ordinal);
        await Assertions.Expect(page.Locator("#loginForm")).ToBeVisibleAsync();

        // The failed attempt is counted by the server.
        long attempts = await Db.ScalarAsync<long>("""SELECT "LoginAttempt" FROM "Account" WHERE "Email" = @email""", ("email", account.Email));
        Assert.Equal(1, attempts);
    }

    /// <summary>A soft-deleted account cannot sign in, even with the right password.</summary>
    [Fact]
    public async Task Login_AsDeletedAccount_IsRefused()
    {
        SeededAccount account = await Db.SeedAccountAsync();
        await Db.ExecuteAsync("""UPDATE "Account" SET "Deleted" = true WHERE "Email" = @email""", ("email", account.Email));
        var page = await NewPageAsync();

        await GotoAsync(page, "/Account/Login");
        await page.Locator("input[name='Email']").FillAsync(account.Email);
        await page.Locator("#Password").FillAsync(account.Password);
        await page.Locator("#btnSignIn").ClickAsync();

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        Assert.Contains("/Account/Login", page.Url, StringComparison.Ordinal);
    }

    /// <summary>Signing out ends the session: a protected page then redirects away.</summary>
    [Fact]
    public async Task Logout_EndsTheSession()
    {
        var (page, _) = await NewSignedInPageAsync();
        await GotoAsync(page, "/AccountBook/Asset");
        await Assertions.Expect(page.Locator("h3.card-title")).ToBeVisibleAsync();

        // The logout control is a POST form (antiforgery token included) inside the user dropdown in
        // the top bar, so open the dropdown first, exactly as a user would.
        await page.Locator("li.user-menu > a.dropdown-toggle").ClickAsync();
        await page.Locator("form[action$='/Account/Logout'] button[type='submit']").ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await GotoAsync(page, "/AccountBook/Asset");
        Assert.DoesNotContain("/AccountBook/Asset", page.Url, StringComparison.Ordinal);
    }

    // -- Role-based access --------------------------------------------------------

    /// <summary>An anonymous visitor cannot open the account book: the server redirects to the landing page.</summary>
    [Theory]
    [InlineData("/AccountBook/Asset")]
    [InlineData("/AccountBook/Income")]
    [InlineData("/AccountBook/Expenditure")]
    [InlineData("/Notice/FixedIncome")]
    [InlineData("/Notice/FixedExpenditure")]
    [InlineData("/Management/Profile")]
    [InlineData("/Management/Account")]
    public async Task ProtectedPage_AsAnonymous_RedirectsAway(string path)
    {
        var page = await NewPageAsync();

        await GotoAsync(page, path);

        Assert.DoesNotContain(path, page.Url, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/Dashboard/AnonymousIndex", page.Url, StringComparison.Ordinal);
    }

    /// <summary>A User cannot open the admin-only management pages.</summary>
    [Theory]
    [InlineData("/Management/Account")]
    [InlineData("/Management/Menu")]
    public async Task AdminPage_AsUser_RedirectsAway(string path)
    {
        var (page, _) = await NewSignedInPageAsync();

        await GotoAsync(page, path);

        Assert.DoesNotContain(path, page.Url, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>An Admin cannot open the User-only account book.</summary>
    [Fact]
    public async Task AccountBook_AsAdmin_RedirectsAway()
    {
        var (page, _) = await NewSignedInPageAsync("Admin");

        await GotoAsync(page, "/AccountBook/Asset");

        Assert.DoesNotContain("/AccountBook/Asset", page.Url, StringComparison.Ordinal);
    }

    /// <summary>Role-restricted static assets are refused to anonymous visitors in every casing (regression for the case-insensitive gate bypass).</summary>
    [Theory]
    [InlineData("/admin/dist/js/adminlte.js")]
    [InlineData("/Admin/dist/js/adminlte.js")]
    [InlineData("/USER/dist/js/adminlte.js")]
    public async Task RestrictedStaticAsset_AsAnonymous_IsForbidden(string path)
    {
        var page = await NewPageAsync();

        var response = await page.APIRequest.GetAsync(BaseUrl + path);

        Assert.Equal(403, response.Status);
    }
}
