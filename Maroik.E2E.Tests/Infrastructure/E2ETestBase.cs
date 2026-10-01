using Microsoft.Playwright;

namespace Maroik.E2E.Tests.Infrastructure;

/// <summary>
/// Base class for Playwright E2E tests.
/// Receives the shared <see cref="E2ESharedFixture"/> from the "E2E" collection,
/// which holds a single <see cref="PlaywrightWebApplicationFactory"/> and browser
/// instance for the entire test session.  Each test creates a fresh
/// <see cref="IPage"/> in a new isolated browser context via <see cref="NewPageAsync"/>.
/// </summary>
public abstract class E2ETestBase(E2ESharedFixture fixture)
{
    /// <summary>Base URL of the Kestrel server the browser talks to.</summary>
    protected string BaseUrl => fixture.ServerBaseUrl;

    /// <summary>The seeded database, for creating fixtures and checking what the server persisted.</summary>
    protected E2EDatabase Db => fixture.Database;

    /// <summary>The in-memory file storage the website reads attachments and images from.</summary>
    protected E2EFileStorage Files => fixture.Files;

    /// <summary>
    /// Signs <paramref name="account"/> in through the real login form and waits until the browser
    /// has left the login page (i.e. the server accepted the credentials and issued a session).
    /// </summary>
    private static async Task LoginAsync(IPage page, SeededAccount account)
    {
        await GotoAsync(page, "/Account/Login");
        await page.WaitForSelectorAsync("#loginForm");

        // The login page pre-fills the public demo account; replace both fields.
        await page.Locator("input[name='Email']").FillAsync(account.Email);
        await page.Locator("#Password").FillAsync(account.Password);
        await page.Locator("#btnSignIn").ClickAsync();

        await page.WaitForURLAsync(url => !url.Contains("/Account/Login", StringComparison.Ordinal),
            new PageWaitForURLOptions { Timeout = 30_000 });
    }

    /// <summary>Seeds an account of <paramref name="role"/>, opens a fresh page and signs it in.</summary>
    protected async Task<(IPage Page, SeededAccount Account)> NewSignedInPageAsync(string role = "User")
    {
        SeededAccount account = await Db.SeedAccountAsync(role);
        IPage page = await NewPageAsync();
        await LoginAsync(page, account);
        return (page, account);
    }

    /// <summary>Creates a fresh <see cref="IPage"/> in a new isolated browser context.</summary>
    protected async Task<IPage> NewPageAsync()
    {
        var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = BaseUrl,
            IgnoreHTTPSErrors = true,
            // Pinned instead of inherited from the machine: the app picks the UI culture (and the
            // default time zone of the register form) from Accept-Language, and Chrome vs Firefox
            // vs a CI runner would otherwise each get a different one.
            Locale = "ko-KR"
        });
        return await context.NewPageAsync();
    }

    /// <summary>
    /// Navigates <paramref name="page"/> to <paramref name="path"/> and waits for the DOM to be ready.
    /// Retries on transient network errors (e.g. <c>net::ERR_NETWORK_CHANGED</c>) that Chromium's
    /// network-change notifier can spuriously raise in sandboxed/virtualized CI environments.
    /// </summary>
    protected static async Task GotoAsync(IPage page, string path)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await page.GotoAsync(path, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = 30_000
                });
                return;
            }
            catch (PlaywrightException ex) when (attempt < maxAttempts && ex.Message.Contains("net::ERR_NETWORK_CHANGED"))
            {
                await Task.Delay(500 * attempt);
            }
        }
    }
}
