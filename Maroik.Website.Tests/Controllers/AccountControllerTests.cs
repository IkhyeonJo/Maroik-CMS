using System.Text.RegularExpressions;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Account;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// Integration smoke tests for <c>AccountController</c>.
/// All self-service account flows (consent, login, register, forgot-password, etc.)
/// are accessible without authentication and should respond with 200 OK.
/// </summary>
[Collection("Website Integration")]
public class AccountControllerTests(MaroikWebApplicationFactory factory)
{
    /// <summary>Client for the shared test host; redirects are not followed so they can be asserted.</summary>
    private readonly HttpClient _client = factory.CreateTestClient(followRedirects: false);

    // -- ConsentForm ----------------------------------------------------------

    /// <summary>Verifies that <c>ConsentForm</c> get when returns200.</summary>
    [Fact]
    public async Task ConsentForm_Get_Returns200()
    {
        var response = await _client.GetAsync("/Account/ConsentForm", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    // -- Login ----------------------------------------------------------------

    /// <summary>Verifies that <c>Login</c> get when returns200.</summary>
    [Fact]
    public async Task Login_Get_Returns200()
    {
        var response = await _client.GetAsync("/Account/Login", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Verifies the session-fixation fix end-to-end: the session cookie issued to an
    /// unauthenticated visitor must NOT still be valid as the session cookie after that same
    /// browser session logs in — a fresh session (and thus a different cookie value) must be
    /// issued on successful authentication.
    /// </summary>
    [Fact]
    public async Task Login_Post_IssuesADifferentSessionCookie_ThanThePreLoginSession()
    {
        const string email = "fixation-test@example.com";
        const string password = "TestPassword1!";

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();
            db.Accounts.Add(new Maroik.Core.PostgreSQL.Models.Account
            {
                Email = email,
                HashedPassword = passwordService.HashPassword(password),
                Nickname = "FixationTester",
                AvatarImagePath = "/upload/Management/Profile/default-avatar.jpg",
                Role = Role.User,
                TimeZoneIanaId = "UTC",
                Locked = false,
                LoginAttempt = 0,
                EmailConfirmed = true,
                AgreedServiceTerms = true,
                Deleted = false,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Simulate a fixation attacker who has already planted a known session cookie in the
        // victim's browser before the victim ever logs in. It must be a cookie the site really
        // issued — a made-up value is ignored by the session middleware (it cannot be unprotected),
        // which would make this test pass even without the fix — so the attacker signs in to their
        // own account and out again, leaving a valid signed-out session to plant.
        AuthenticatedSession attacker = await AuthenticatedSessionHelper.LoginAsync(
            factory, _client, $"fixation-attacker-{Guid.NewGuid():N}@example.com", "AttackerPassword1!", Role.User,
            TestContext.Current.CancellationToken);
        using (var logout = attacker.BuildJsonPostRequest("/Account/Logout"))
            Assert.Equal(System.Net.HttpStatusCode.Redirect, (await _client.SendAsync(logout, TestContext.Current.CancellationToken)).StatusCode);
        string attackerChosenSessionId = attacker.CookieHeader.Split(';')[0].Split('=', 2)[1];

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, "/Account/Login");
        getRequest.Headers.Add("Cookie", $"{SessionCookieName}={attackerChosenSessionId}");
        var getResponse = await _client.SendAsync(getRequest, TestContext.Current.CancellationToken);
        string html = await getResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        string antiForgeryToken = ExtractAntiForgeryToken(html);
        string? antiForgeryCookie = ExtractCookieValue(getResponse, AntiForgeryCookieName);
        Assert.False(string.IsNullOrEmpty(antiForgeryCookie));

        // POST valid credentials while still presenting the attacker's session cookie, exactly as
        // the victim's browser would (it has no way to know that cookie was planted by an attacker).
        using var postRequest = new HttpRequestMessage(HttpMethod.Post, "/Account/Login");
        postRequest.Headers.Add("Cookie", $"{SessionCookieName}={attackerChosenSessionId}; {AntiForgeryCookieName}={antiForgeryCookie}");
        postRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = antiForgeryToken
        });
        var postResponse = await _client.SendAsync(postRequest, TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, postResponse.StatusCode); // confirms login actually succeeded (failure re-renders the form as 200)
        string? postLoginSessionCookie = ExtractCookieValue(postResponse, SessionCookieName);
        Assert.False(string.IsNullOrEmpty(postLoginSessionCookie));
        // The core assertion: the attacker's pre-chosen session ID must never become the
        // authenticated session — if it did, the attacker's own copy of that same cookie would
        // now be logged in as the victim.
        Assert.NotEqual(attackerChosenSessionId, postLoginSessionCookie);
    }

    // -- Test helpers -----------------------------------------------------------

    // Matches Program.cs: const cookiePrefix = "__Secure-" + SessionDefaults.CookieName / AntiforgeryOptions.DefaultCookiePrefix.
    private const string SessionCookieName = "__Secure-.AspNetCore.Session";
    /// <summary>Name prefix of the antiforgery cookie the site issues.</summary>
    private const string AntiForgeryCookieName = "__Secure-.AspNetCore.Antiforgery.";

    /// <summary>Extracts the value of a specific cookie from a response's Set-Cookie headers, or null if absent.</summary>
    private static string? ExtractCookieValue(HttpResponseMessage response, string cookieName)
    {
        return !response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders) ? null : (from header in setCookieHeaders select header.Split(';', 2)[0] into namePart let eq = namePart.IndexOf('=') where eq > 0 where namePart[..eq] == cookieName select namePart[(eq + 1)..]).FirstOrDefault();
    }

    /// <summary>Extracts the antiforgery hidden-input value from a rendered form's HTML.</summary>
    private static string ExtractAntiForgeryToken(string html)
    {
 #pragma warning disable SYSLIB1045
        Match match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
 #pragma warning restore SYSLIB1045
        Assert.True(match.Success, "Could not find __RequestVerificationToken in the login page HTML.");
        return match.Groups[1].Value;
    }

    // -- Logout ---------------------------------------------------------------

    /// <summary>
    /// Verifies that <c>Logout</c> is a POST + antiforgery endpoint (so it cannot be triggered by a
    /// cross-site link/image) and, when called correctly, clears the session and redirects to the
    /// dashboard.
    /// </summary>
    [Fact]
    public async Task Logout_Post_WithAntiforgery_RedirectsToDashboard()
    {
        // Grab an antiforgery cookie/token pair from any GET page.
        using var getRequest = new HttpRequestMessage(HttpMethod.Get, "/Account/Login");
        var getResponse = await _client.SendAsync(getRequest, TestContext.Current.CancellationToken);
        string html = await getResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        string antiForgeryToken = ExtractAntiForgeryToken(html);
        string? antiForgeryCookie = ExtractCookieValue(getResponse, AntiForgeryCookieName);
        Assert.False(string.IsNullOrEmpty(antiForgeryCookie));

        using var postRequest = new HttpRequestMessage(HttpMethod.Post, "/Account/Logout");
        postRequest.Headers.Add("Cookie", $"{AntiForgeryCookieName}={antiForgeryCookie}");
        postRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiForgeryToken
        });
        var response = await _client.SendAsync(postRequest, TestContext.Current.CancellationToken);

        // Logout clears session and redirects to "/" (default route resolves Dashboard/AnonymousIndex)
        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
    }

    /// <summary>A bare GET to <c>/Account/Logout</c> must not log anyone out — the action is POST-only now.</summary>
    [Fact]
    public async Task Logout_Get_IsNotAllowed()
    {
        var response = await _client.GetAsync("/Account/Logout", TestContext.Current.CancellationToken);

        Assert.NotEqual(System.Net.HttpStatusCode.Redirect, response.StatusCode);
    }

    // -- Register -------------------------------------------------------------

    /// <summary>Verifies that <c>Register</c> get when returns200.</summary>
    [Fact]
    public async Task Register_Get_Returns200()
    {
        var response = await _client.GetAsync("/Account/Register", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    // -- ConfirmEmail ---------------------------------------------------------

    /// <summary>Verifies that <c>ConfirmEmail</c> get when returns 200 with invalid token.</summary>
    [Fact]
    public async Task ConfirmEmail_Get_Returns200_WithInvalidToken()
    {
        // Providing an invalid/fake token; the controller should handle decryption failure
        // and show the view with InvalidToken = true rather than throwing.
        var response = await _client.GetAsync("/Account/ConfirmEmail?registrationToken=invalid-token", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    // -- ForgotPassword -------------------------------------------------------

    /// <summary>Verifies that <c>ForgotPassword</c> get when returns200.</summary>
    [Fact]
    public async Task ForgotPassword_Get_Returns200()
    {
        var response = await _client.GetAsync("/Account/ForgotPassword", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    // -- ResetPassword --------------------------------------------------------

    /// <summary>Verifies that <c>ResetPassword</c> get when returns 200 with invalid token.</summary>
    [Fact]
    public async Task ResetPassword_Get_Returns200_WithInvalidToken()
    {
        // Providing an invalid/fake token; ValidateResetPasswordTokenAsync should catch
        // the decryption failure and show the view with FailToReset = true.
        var response = await _client.GetAsync("/Account/ResetPassword?resetPasswordToken=invalid-token", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }
}
