using Maroik.E2E.Tests.Infrastructure;
using Microsoft.Playwright;

namespace Maroik.E2E.Tests.Flows;

/// <summary>
/// End-to-end browser tests for the Account self-service flows.
///
/// Each test launches headless Chromium via Playwright against the real Kestrel server
/// started by <see cref="PlaywrightWebApplicationFactory"/> (backed by a real PostgreSQL
/// container). Tests mostly verify that the correct HTML elements render; the sign-in /
/// registration POST flows are not exercised (they need working SMTP), but the antiforgery-
/// protected Logout POST is.
/// </summary>
[Collection("E2E")]
public class AccountFlowTests(E2ESharedFixture fixture) : E2ETestBase(fixture)
{
    // -- Login ----------------------------------------------------------------

    /// <summary>Verifies that <c>LoginPage</c> has email and password inputs and submit button.</summary>
    [Fact]
    public async Task LoginPage_HasEmailAndPasswordInputsAndSubmitButton()
    {
        var page = await NewPageAsync();
        await GotoAsync(page, "/Account/Login");

        // The login form exists with id="loginForm"
        await page.WaitForSelectorAsync("#loginForm");

        // Both credential inputs are rendered
        await Assertions.Expect(page.Locator("input[name='Email']")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#Password")).ToBeVisibleAsync();

        // The sign-in submit button is present and enabled
        await Assertions.Expect(page.Locator("#btnSignIn")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#btnSignIn")).ToBeEnabledAsync();
    }

    /// <summary>Verifies that <c>LoginPage</c> has title.</summary>
    [Fact]
    public async Task LoginPage_HasTitle()
    {
        var page = await NewPageAsync();
        await GotoAsync(page, "/Account/Login");

        // The browser page title must be non-empty
        string title = await page.TitleAsync();
        Assert.False(string.IsNullOrWhiteSpace(title));
    }

    // -- Register -------------------------------------------------------------

    /// <summary>Verifies that <c>RegisterPage</c> has registration form with all fields.</summary>
    [Fact]
    public async Task RegisterPage_HasRegistrationFormWithAllFields()
    {
        var page = await NewPageAsync();
        await GotoAsync(page, "/Account/Register");

        await page.WaitForSelectorAsync("#registerForm");

        // All required registration fields are visible
        await Assertions.Expect(page.Locator("#Nickname")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#Email")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#Password")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#btnRegister")).ToBeVisibleAsync();
    }

    // -- ConsentForm ----------------------------------------------------------

    /// <summary>Verifies that <c>ConsentFormPage</c> loads with non-empty body.</summary>
    [Fact]
    public async Task ConsentFormPage_Loads_WithNonEmptyBody()
    {
        var page = await NewPageAsync();
        await GotoAsync(page, "/Account/ConsentForm");

        // Page loaded successfully — body element must have content
        string bodyText = await page.Locator("body").InnerTextAsync();
        Assert.False(string.IsNullOrWhiteSpace(bodyText));
    }

    // -- ForgotPassword -------------------------------------------------------

    /// <summary>Verifies that <c>ForgotPasswordPage</c> has email input and submit button.</summary>
    [Fact]
    public async Task ForgotPasswordPage_HasEmailInputAndSubmitButton()
    {
        var page = await NewPageAsync();
        await GotoAsync(page, "/Account/ForgotPassword");

        // The email input and submit button must both be rendered
        await Assertions.Expect(page.Locator("input[name='Email']")).ToBeVisibleAsync();

        var submitButton = page.Locator("button[type='submit']");
        await Assertions.Expect(submitButton).ToBeVisibleAsync();
    }

    /// <summary>
    /// A second reset request for the same address inside the cooldown window is bounced back to the
    /// form, and the "wait a moment" notice the throttle leaves in TempData is actually shown — it used
    /// to be set but never rendered, so the visitor just saw the same empty form.
    /// </summary>
    [Fact]
    public async Task ForgotPassword_SecondRequestForTheSameAddress_ShowsTheCooldownNotice()
    {
        var page = await NewPageAsync();
        string email = $"cooldown-{Guid.NewGuid():N}"[..20] + "@test.com";

        // First request: accepted (the address is unknown, so nothing is mailed) -> confirmation page, no notice.
        await SubmitAsync();
        await Assertions.Expect(page.Locator("#forgotPasswordForm")).ToHaveCountAsync(0);

        // Second request inside the window: back on the form, with the notice (ko-KR, see E2ETestBase).
        await SubmitAsync();
        await Assertions.Expect(page.Locator("#forgotPasswordForm")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#forgotPasswordError"))
            .ToContainTextAsync("잠시 후에 다시 이메일을 요청해 주세요.");
        return;

        async Task SubmitAsync()
        {
            await GotoAsync(page, "/Account/ForgotPassword");
            await page.Locator("input[name='Email']").FillAsync(email);
            await page.RunAndWaitForResponseAsync(
                () => page.Locator("#btnRequestNewPassword").ClickAsync(),
                response => response.Request.Method == "POST" && response.Url.Contains("/Account/ForgotPassword", StringComparison.Ordinal));
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }
    }

    /// <summary>Opening the form without a pending notice shows no error text.</summary>
    [Fact]
    public async Task ForgotPasswordPage_WithoutAPendingNotice_ShowsNoError()
    {
        var page = await NewPageAsync();
        await GotoAsync(page, "/Account/ForgotPassword");

        await Assertions.Expect(page.Locator("#forgotPasswordError")).ToHaveTextAsync("");
    }

    // -- ConfirmEmail (invalid token) -----------------------------------------

    /// <summary>Verifies that <c>ConfirmEmailPage</c> with invalid token when loads without unhandled error.</summary>
    [Fact]
    public async Task ConfirmEmailPage_WithInvalidToken_LoadsWithoutUnhandledError()
    {
        var page = await NewPageAsync();

        // Track uncaught JavaScript exceptions (not console noise such as a missing favicon)
        var jsErrors = new List<string>();
        page.PageError += (_, message) => jsErrors.Add(message);

        await GotoAsync(page, "/Account/ConfirmEmail?registrationToken=bad-token");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Page must render (no 500 crash) and its scripts must not have thrown
        string bodyText = await page.Locator("body").InnerTextAsync();
        Assert.False(string.IsNullOrWhiteSpace(bodyText));
        Assert.Empty(jsErrors);
    }

    // -- ResetPassword (invalid token) ----------------------------------------

    /// <summary>Verifies that <c>ResetPasswordPage</c> with invalid token when loads without unhandled error.</summary>
    [Fact]
    public async Task ResetPasswordPage_WithInvalidToken_LoadsWithoutUnhandledError()
    {
        var page = await NewPageAsync();
        await GotoAsync(page, "/Account/ResetPassword?resetPasswordToken=bad-token");

        string bodyText = await page.Locator("body").InnerTextAsync();
        Assert.False(string.IsNullOrWhiteSpace(bodyText));
    }

    // -- Logout (POST) → redirect ------------------------------------------------

    /// <summary>
    /// Verifies that a POST to <c>Logout</c> — the only verb it accepts
    /// (<c>[HttpPost] [ValidateAntiForgeryToken]</c>) — clears the session and redirects to the
    /// anonymous dashboard. The real UI logout control is a <c>method="post"</c> form
    /// (<c>_MainTopBar.cshtml</c>), so reproduce that instead of navigating (which is a GET).
    /// </summary>
    [Fact]
    public async Task Logout_Post_RedirectsToAnonymousDashboard()
    {
        var page = await NewPageAsync();

        // Any page with a POST form renders the antiforgery token and sets its cookie; the login
        // page's form is the simplest. Reuse its token for the injected logout form.
        await GotoAsync(page, "/Account/Login");
        string token = await page.Locator("input[name='__RequestVerificationToken']").First.InputValueAsync();

        await page.EvaluateAsync(
            """
            (token) => {
                            const form = document.createElement('form');
                            form.method = 'POST';
                            form.action = '/Account/Logout';
                            const input = document.createElement('input');
                            input.type = 'hidden';
                            input.name = '__RequestVerificationToken';
                            input.value = token;
                            form.appendChild(input);
                            document.body.appendChild(form);
                            form.submit();
                        }
            """,
            token);

        // RedirectToAction("AnonymousIndex","Dashboard") collapses to "/" (default-route defaults);
        // the URL generator preserves casing and this app does not lowercase URLs, so the path is
        // exactly "/".
        await page.WaitForURLAsync(
            url => new Uri(url).AbsolutePath == "/",
            new PageWaitForURLOptions { Timeout = 15_000 });

        string bodyText = await page.Locator("body").InnerTextAsync();
        Assert.False(string.IsNullOrWhiteSpace(bodyText));
    }
}
