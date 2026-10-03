using System.Text.RegularExpressions;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Account;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// Integration tests for the business-logic-bearing POST actions of <c>AccountController</c> —
/// <c>Register</c>, <c>ConfirmEmail</c>, <c>ForgotPassword</c>, <c>ResetPassword</c> — that
/// <c>AccountControllerTests</c> deliberately left uncovered beyond their GET/invalid-token
/// smoke tests. These four flows carry real controller-level branching (consent/timezone
/// checks, the shared <c>LoginInputViewModel</c>'s per-action <c>ModelState.Remove</c> calls,
/// register-vs-resend routing) that this session has repeatedly found bugs in elsewhere
/// (<c>ManagementController</c>'s TimeZoneIanaId/Password dead-code fixes), so they were worth
/// closing rather than leaving as smoke-only.
///
/// Two things had to be fixed in <see cref="MaroikWebApplicationFactory"/> before any of this
/// was even testable: the RSA key settings were empty strings (every one of these flows
/// encrypts/decrypts a token, so all four would throw "RSA private/public key is not
/// configured"), and there was no substitute for the RabbitMQ-backed <c>IEmailPublisher</c>
/// (Register/ForgotPassword both publish a confirmation/reset email). Both are now real: a
/// freshly generated RSA keypair, and <see cref="FakeEmailPublisher"/> recording what would have
/// been sent.
/// </summary>
[Collection("Website Integration")]
public class AccountControllerRegistrationTests(MaroikWebApplicationFactory factory)
{
    /// <summary>Client for the shared test host; redirects are not followed so they can be asserted.</summary>
    private readonly HttpClient _client = factory.CreateTestClient(followRedirects: false);

    /// <summary>An e-mail unique to this call, prefixed with the calling test's name.</summary>
    private static string UniqueEmail([System.Runtime.CompilerServices.CallerMemberName] string testName = "") =>
        $"{testName.ToLowerInvariant()}-{Guid.NewGuid():N}@test.com";

    /// <summary>Fetches a page's antiforgery cookie + hidden-input token pair for an anonymous POST.</summary>
    private async Task<(string Cookie, string Token)> GetAntiForgeryAsync(string getUrl)
    {
        var response = await _client.GetAsync(getUrl, TestContext.Current.CancellationToken);
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        string token = ExtractAntiForgeryToken(html);
        string? cookie = ExtractCookieValue(response, AntiForgeryCookieName);
        Assert.False(string.IsNullOrEmpty(cookie));
        return (cookie, token);
    }

    /// <summary>Posts <paramref name="fields"/> as a form to <paramref name="url"/> with a fresh antiforgery cookie and token taken from that page.</summary>
    private async Task<HttpResponseMessage> PostFormAsync(string url, Dictionary<string, string> fields)
    {
        var (cookie, token) = await GetAntiForgeryAsync(url);
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Cookie", $"{AntiForgeryCookieName}={cookie}");
        fields["__RequestVerificationToken"] = token;
        request.Content = new FormUrlEncodedContent(fields);
        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>Name prefix of the antiforgery cookie the site issues.</summary>
    private const string AntiForgeryCookieName = "__Secure-.AspNetCore.Antiforgery.";

    /// <summary>Name of the session cookie the site issues (see Program.cs).</summary>
    private const string SessionCookieName = "__Secure-.AspNetCore.Session";

    /// <summary>The value of <paramref name="cookieName"/> from the response's <c>Set-Cookie</c> headers, or <see langword="null"/>.</summary>
    private static string? ExtractCookieValue(HttpResponseMessage response, string cookieName)
    {
        return !response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders) ? null : (from header in setCookieHeaders select header.Split(';', 2)[0] into namePart let eq = namePart.IndexOf('=') where eq > 0 where namePart[..eq] == cookieName select namePart[(eq + 1)..]).FirstOrDefault();
    }

    /// <summary>The hidden <c>__RequestVerificationToken</c> value in <paramref name="html"/>; fails the test if there is none.</summary>
    private static string ExtractAntiForgeryToken(string html)
    {
 #pragma warning disable SYSLIB1045
        Match match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
 #pragma warning restore SYSLIB1045
        Assert.True(match.Success, "Could not find __RequestVerificationToken in the page HTML.");
        return match.Groups[1].Value;
    }

    /// <summary>Inserts a User account for <paramref name="email"/> directly into the database with the given confirmation state and tokens.</summary>
    private async Task SeedAccountAsync(string email, bool emailConfirmed, string? registrationToken = null, string? resetPasswordToken = null, string? nickname = null, bool agreedServiceTerms = true)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        db.Accounts.Add(new Maroik.Core.PostgreSQL.Models.Account
        {
            Email = email,
            HashedPassword = passwordService.HashPassword("OldPassword1!"),
            Nickname = nickname ?? "RegTest_" + email.Replace("@", "_").Replace(".", "_"),
            AvatarImagePath = "/upload/Management/Profile/default-avatar.jpg",
            Role = Role.User,
            TimeZoneIanaId = "UTC",
            Locked = false,
            LoginAttempt = 0,
            EmailConfirmed = emailConfirmed,
            AgreedServiceTerms = agreedServiceTerms,
            RegistrationToken = registrationToken,
            ResetPasswordToken = resetPasswordToken,
            Deleted = false,
            Created = DateTime.UtcNow,
            Updated = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    // -- Register: success path -----------------------------------------------------

    /// <summary>Register new account creates unconfirmed account and publishes email.</summary>
    [Fact]
    public async Task Register_NewAccount_CreatesUnconfirmedAccountAndPublishesEmail()
    {
        string email = UniqueEmail();
        var response = await PostFormAsync("/Account/Register", new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = "NewPassword1!",
            ["Nickname"] = "RegSuccess_" + Guid.NewGuid().ToString("N")[..8],
            ["AgreedServiceTerms"] = "true",
            ["TimeZoneIanaId"] = "UTC"
        });

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var created = db.Accounts.Single(a => a.Email == email);
        Assert.False(created.EmailConfirmed);
        Assert.False(string.IsNullOrEmpty(created.RegistrationToken));

        var publisher = scope.ServiceProvider.GetRequiredService<FakeEmailPublisher>();
        Assert.Contains(publisher.PublishedMessages, m => m.ToEmail == email);
    }

    /// <summary>Register missing consent does not create account.</summary>
    [Fact]
    public async Task Register_MissingConsent_DoesNotCreateAccount()
    {
        string email = UniqueEmail();
        var response = await PostFormAsync("/Account/Register", new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = "NewPassword1!",
            ["Nickname"] = "RegNoConsent_" + Guid.NewGuid().ToString("N")[..8],
            ["AgreedServiceTerms"] = "false",
            ["TimeZoneIanaId"] = "UTC"
        });

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(db.Accounts.Any(a => a.Email == email));
    }

    /// <summary>Register missing time zone does not create account.</summary>
    [Fact]
    public async Task Register_MissingTimeZone_DoesNotCreateAccount()
    {
        string email = UniqueEmail();
        var response = await PostFormAsync("/Account/Register", new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = "NewPassword1!",
            ["Nickname"] = "RegNoTz_" + Guid.NewGuid().ToString("N")[..8],
            ["AgreedServiceTerms"] = "true",
            ["TimeZoneIanaId"] = ""
        });

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(db.Accounts.Any(a => a.Email == email));
    }

    /// <summary>Register already confirmed email does not overwrite account.</summary>
    [Fact]
    public async Task Register_AlreadyConfirmedEmail_DoesNotOverwriteAccount()
    {
        string email = UniqueEmail();
        await SeedAccountAsync(email, emailConfirmed: true);

        var response = await PostFormAsync("/Account/Register", new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = "AnotherPassword1!",
            ["Nickname"] = "RegDup_" + Guid.NewGuid().ToString("N")[..8],
            ["AgreedServiceTerms"] = "true",
            ["TimeZoneIanaId"] = "UTC"
        });

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var account = db.Accounts.Single(a => a.Email == email);
        Assert.True(account.EmailConfirmed); // unchanged
    }

    /// <summary>
    /// The "nickname already exists" message is stored raw in <c>TempData</c> and HTML-encoded once by the
    /// view. Regression: it used to be encoded twice (once when built, once by the view), so a Korean
    /// nickname was shown as <c>&amp;#xD64D;…</c> and <c>R&amp;D</c> as <c>R&amp;amp'd</c>.
    /// </summary>
    [Theory]
    [InlineData("홍길동")]
    [InlineData("R&D팀")]
    [InlineData("O'Brien")]
    [InlineData("PlainAscii")]
    public async Task Register_TakenNickname_ShowsTheNicknameEncodedOnlyOnce(string nickname)
    {
        await SeedAccountAsync(UniqueEmail(), emailConfirmed: true, nickname: nickname);

        var response = await PostFormAsync("/Account/Register", new Dictionary<string, string>
        {
            ["Email"] = UniqueEmail(),
            ["Password"] = "AnotherPassword1!",
            ["ConfirmPassword"] = "AnotherPassword1!",
            ["Nickname"] = nickname,
            ["AgreedServiceTerms"] = "true",
            ["TimeZoneIanaId"] = "UTC"
        });

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
 #pragma warning disable SYSLIB1045
        Match error = Regex.Match(html, "<span style=\"font-weight:bold; color: red; \">(.*?)</span>", RegexOptions.Singleline);
 #pragma warning restore SYSLIB1045
        Assert.True(error.Success, "The registration error message was not rendered.");

        // What the browser shows is the HTML-decoded text; it must read exactly like the typed nickname.
        Assert.StartsWith($"'{nickname}' ", System.Net.WebUtility.HtmlDecode(error.Groups[1].Value));
    }

    /// <summary>
    /// The "not a valid email address" message carries the typed address as a format argument. It must
    /// read like the typed text (once-encoded), not with entities: <c>+</c>, <c>'</c> and <c>@</c> are all
    /// characters the default HTML encoder escapes. "user@localhost" passes the view model's
    /// <c>[EmailAddress]</c> attribute but is rejected by the domain's <c>Email</c> value object, which is
    /// what surfaces the message.
    /// </summary>
    [Fact]
    public async Task Register_InvalidEmailForDomain_ShowsTheAddressEncodedOnlyOnce()
    {
        const string typed = "o'brien+x@localhost";

        var response = await PostFormAsync("/Account/Register", new Dictionary<string, string>
        {
            ["Email"] = typed,
            ["Password"] = "AnotherPassword1!",
            ["ConfirmPassword"] = "AnotherPassword1!",
            ["Nickname"] = "EmailMsg_" + Guid.NewGuid().ToString("N")[..8],
            ["AgreedServiceTerms"] = "true",
            ["TimeZoneIanaId"] = "UTC"
        });

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
 #pragma warning disable SYSLIB1045
        Match error = Regex.Match(html, "<span style=\"font-weight:bold; color: red; \">(.*?)</span>", RegexOptions.Singleline);
 #pragma warning restore SYSLIB1045
        Assert.True(error.Success, "The registration error message was not rendered.");

        Assert.StartsWith($"'{typed}' ", System.Net.WebUtility.HtmlDecode(error.Groups[1].Value));
    }

    /// <summary>Register unconfirmed email resends confirmation on resubmission.</summary>
    [Fact]
    public async Task Register_UnconfirmedEmail_ResendsConfirmationOnResubmission()
    {
        string email = UniqueEmail();
        await SeedAccountAsync(email, emailConfirmed: false, registrationToken: null);

        // Resend path: submitting with an empty Password re-triggers the confirmation email
        // rather than attempting to create a second account.
        var response = await PostFormAsync("/Account/Register", new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = "",
            ["Nickname"] = "",
            ["AgreedServiceTerms"] = "false",
            ["TimeZoneIanaId"] = ""
        });

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var account = db.Accounts.Single(a => a.Email == email);
        Assert.False(string.IsNullOrEmpty(account.RegistrationToken)); // regenerated

        var publisher = scope.ServiceProvider.GetRequiredService<FakeEmailPublisher>();
        Assert.Contains(publisher.PublishedMessages, m => m.ToEmail == email);
    }

    /// <summary>
    /// The "you keep failing to receive the mail" state links the administrator with a well-formed
    /// <c>mailto:</c> URL (it once had a stray space after the colon).
    /// </summary>
    [Fact]
    public async Task Register_UnconfirmedEmail_Resubmission_ShowsAWellFormedAdministratorMailtoLink()
    {
        string email = UniqueEmail();
        await SeedAccountAsync(email, emailConfirmed: false, registrationToken: null);

        var response = await PostFormAsync("/Account/Register", new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = "",
            ["Nickname"] = "",
            ["AgreedServiceTerms"] = "false",
            ["TimeZoneIanaId"] = ""
        });
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("mailto:admin@maroik.com", html);
        Assert.DoesNotContain("mailto: ", html);
    }

    // -- ConfirmEmail -----------------------------------------------------------------

    /// <summary>Encrypts <paramref name="rawToken"/> with the host's RSA service, as the mailed link carries it.</summary>
    private string EncryptToken(string rawToken)
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IRsaService>().Encrypt(rawToken);
    }

    /// <summary>The stored <c>EmailConfirmed</c> flag of <paramref name="email"/>.</summary>
    private bool IsEmailConfirmed(string email)
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Accounts.Single(a => a.Email == email).EmailConfirmed;
    }

    /// <summary>Opens the confirmation link, then submits its password form (antiforgery taken from that page).</summary>
    private async Task<HttpResponseMessage> SubmitConfirmationAsync(string encryptedToken, string password)
    {
        string url = $"/Account/ConfirmEmail?registrationToken={Uri.EscapeDataString(encryptedToken)}";
        var (cookie, token) = await GetAntiForgeryAsync(url);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Account/ConfirmEmail");
        request.Headers.Add("Cookie", $"{AntiForgeryCookieName}={cookie}");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["RegistrationToken"] = encryptedToken,
            ["Password"] = password,
            ["__RequestVerificationToken"] = token
        });
        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>Opening a live confirmation link shows the password form and activates nothing on its own.</summary>
    [Fact]
    public async Task ConfirmEmail_Get_ValidToken_ShowsThePasswordForm_AndDoesNotActivate()
    {
        string email = UniqueEmail();
        string rawToken = GuidToken.Generate(DateTime.UtcNow);
        await SeedAccountAsync(email, emailConfirmed: false, registrationToken: rawToken);

        var response = await _client.GetAsync($"/Account/ConfirmEmail?registrationToken={Uri.EscapeDataString(EncryptToken(rawToken))}", TestContext.Current.CancellationToken);
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"confirmEmailForm\"", html);
        Assert.False(IsEmailConfirmed(email));
    }

    /// <summary>The link plus the password chosen at registration activates the account.</summary>
    [Fact]
    public async Task ConfirmEmail_Post_RegistrationPassword_ActivatesAccount()
    {
        string email = UniqueEmail();
        string rawToken = GuidToken.Generate(DateTime.UtcNow);
        await SeedAccountAsync(email, emailConfirmed: false, registrationToken: rawToken);

        var response = await SubmitConfirmationAsync(EncryptToken(rawToken), "OldPassword1!");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.True(IsEmailConfirmed(email));
    }

    /// <summary>
    /// Pre-hijacking guard: the link with any other password leaves the account unconfirmed and
    /// re-shows the form, so an address someone else registered cannot be activated by its owner's click.
    /// </summary>
    [Fact]
    public async Task ConfirmEmail_Post_WrongPassword_DoesNotActivate_AndReShowsTheForm()
    {
        string email = UniqueEmail();
        string rawToken = GuidToken.Generate(DateTime.UtcNow);
        await SeedAccountAsync(email, emailConfirmed: false, registrationToken: rawToken);

        var response = await SubmitConfirmationAsync(EncryptToken(rawToken), "SomeoneElses1!");
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"confirmEmailForm\"", html);
        Assert.False(IsEmailConfirmed(email));
    }

    /// <summary>
    /// Pre-hijacking guard end to end: re-registering an unconfirmed address replaces the earlier
    /// registrant's password, so only the latest registrant's password can activate it.
    /// </summary>
    [Fact]
    public async Task Register_UnconfirmedEmail_ReplacesTheEarlierRegistrantsPassword()
    {
        string email = UniqueEmail();
        await SeedAccountAsync(email, emailConfirmed: false, registrationToken: GuidToken.Generate(DateTime.UtcNow));

        var response = await PostFormAsync("/Account/Register", new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = "OwnersPassword1!",
            ["Nickname"] = "RegOwner_" + Guid.NewGuid().ToString("N")[..8],
            ["AgreedServiceTerms"] = "true",
            ["TimeZoneIanaId"] = "UTC"
        });

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var account = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Accounts.Single(a => a.Email == email);
        Assert.True(passwords.VerifyPassword("OwnersPassword1!", account.HashedPassword));
        Assert.False(passwords.VerifyPassword("OldPassword1!", account.HashedPassword));
        Assert.False(account.EmailConfirmed);
    }

    /// <summary>Confirm email already confirmed does not error.</summary>
    [Fact]
    public async Task ConfirmEmail_AlreadyConfirmed_DoesNotError()
    {
        string email = UniqueEmail();
        string rawToken = GuidToken.Generate(DateTime.UtcNow);
        await SeedAccountAsync(email, emailConfirmed: true, registrationToken: rawToken);

        var response = await _client.GetAsync($"/Account/ConfirmEmail?registrationToken={Uri.EscapeDataString(EncryptToken(rawToken))}", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    // -- ForgotPassword -----------------------------------------------------------------

    /// <summary>Forgot password confirmed account sets reset token and publishes email.</summary>
    [Fact]
    public async Task ForgotPassword_ConfirmedAccount_SetsResetTokenAndPublishesEmail()
    {
        string email = UniqueEmail();
        await SeedAccountAsync(email, emailConfirmed: true);

        var response = await PostFormAsync("/Account/ForgotPassword", new Dictionary<string, string> { ["Email"] = email });

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var account = db.Accounts.Single(a => a.Email == email);
        Assert.False(string.IsNullOrEmpty(account.ResetPasswordToken));

        var publisher = scope.ServiceProvider.GetRequiredService<FakeEmailPublisher>();
        Assert.Contains(publisher.PublishedMessages, m => m.ToEmail == email);
    }

    /// <summary>Forgot password unknown email silently succeeds without publishing.</summary>
    [Fact]
    public async Task ForgotPassword_UnknownEmail_SilentlySucceedsWithoutPublishing()
    {
        string email = UniqueEmail();

        var response = await PostFormAsync("/Account/ForgotPassword", new Dictionary<string, string> { ["Email"] = email });

        // Silently succeeds (200) to avoid user enumeration, even though the account doesn't exist.
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<FakeEmailPublisher>();
        Assert.DoesNotContain(publisher.PublishedMessages, m => m.ToEmail == email);
    }

    // -- ResetPassword ------------------------------------------------------------------

    /// <summary>Reset password get valid token does not fail.</summary>
    [Fact]
    public async Task ResetPassword_Get_ValidToken_DoesNotFail()
    {
        string email = UniqueEmail();
        string rawToken = GuidToken.Generate(DateTime.UtcNow);
        await SeedAccountAsync(email, emailConfirmed: true, resetPasswordToken: rawToken);

        using var scope = factory.Services.CreateScope();
        var rsa = scope.ServiceProvider.GetRequiredService<IRsaService>();
        string encryptedToken = rsa.Encrypt(rawToken);

        var response = await _client.GetAsync($"/Account/ResetPassword?resetPasswordToken={Uri.EscapeDataString(encryptedToken)}", TestContext.Current.CancellationToken);
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        // A valid token renders the new-password form (id="resetPasswordForm"); an invalid one
        // renders "The authentication token is invalid." instead — see ResetPassword.cshtml.
        Assert.Contains("resetPasswordForm", html);
    }

    /// <summary>Reset password post valid token changes password and allows login with new password.</summary>
    [Fact]
    public async Task ResetPassword_Post_ValidToken_ChangesPasswordAndAllowsLoginWithNewPassword()
    {
        string email = UniqueEmail();
        string rawToken = GuidToken.Generate(DateTime.UtcNow);
        await SeedAccountAsync(email, emailConfirmed: true, resetPasswordToken: rawToken);

        using var scope = factory.Services.CreateScope();
        var rsa = scope.ServiceProvider.GetRequiredService<IRsaService>();
        string encryptedToken = rsa.Encrypt(rawToken);

        var response = await PostFormAsync("/Account/ResetPassword", new Dictionary<string, string>
        {
            ["ResetPasswordToken"] = encryptedToken,
            ["Password"] = "BrandNewPassword1!"
        });
        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);

        // Prove the new password actually works end-to-end via the real Login action.
        var (loginCookie, loginToken) = await GetAntiForgeryAsync("/Account/Login");
        using var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/Account/Login");
        loginRequest.Headers.Add("Cookie", $"{AntiForgeryCookieName}={loginCookie}");
        loginRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = "BrandNewPassword1!",
            ["__RequestVerificationToken"] = loginToken
        });
        var loginResponse = await _client.SendAsync(loginRequest, TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, loginResponse.StatusCode); // login success redirects; failure re-renders as 200
    }

    /// <summary>
    /// A successful reset signs the user in straight away, the way a successful login does: it redirects to the
    /// dashboard with a new session cookie, and that session opens a signed-in-only page. The browser brings a
    /// session cookie an attacker planted: a real one the site issued (the attacker signed in and out, leaving a
    /// valid signed-out session — a made-up value would be ignored by the session middleware anyway). That
    /// session must not be the one that gets signed in.
    /// </summary>
    [Fact]
    public async Task ResetPassword_Post_ValidToken_SignsTheUserIn_WithAFreshSession()
    {
        string email = UniqueEmail();
        string rawToken = GuidToken.Generate(DateTime.UtcNow);
        await SeedAccountAsync(email, emailConfirmed: true, resetPasswordToken: rawToken);
        AuthenticatedSession attacker = await AuthenticatedSessionHelper.LoginAsync(
            factory, _client, UniqueEmail(), "AttackerPassword1!", Role.User, TestContext.Current.CancellationToken);
        using (var logout = attacker.BuildJsonPostRequest("/Account/Logout"))
            Assert.Equal(System.Net.HttpStatusCode.Redirect, (await _client.SendAsync(logout, TestContext.Current.CancellationToken)).StatusCode);
        string plantedSessionId = attacker.CookieHeader.Split(';')[0].Split('=', 2)[1];
        var (cookie, token) = await GetAntiForgeryAsync("/Account/ResetPassword");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Account/ResetPassword");
        request.Headers.Add("Cookie", $"{SessionCookieName}={plantedSessionId}; {AntiForgeryCookieName}={cookie}");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ResetPasswordToken"] = EncryptToken(rawToken),
            ["Password"] = "BrandNewPassword1!",
            ["__RequestVerificationToken"] = token
        });

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString); // the default route: Dashboard/AnonymousIndex, where Login sends a user too
        string? session = ExtractCookieValue(response, SessionCookieName);
        Assert.False(string.IsNullOrEmpty(session));
        Assert.NotEqual(plantedSessionId, session);

        using var page = new HttpRequestMessage(HttpMethod.Get, "/AccountBook/Income");
        page.Headers.Add("Cookie", $"{SessionCookieName}={session}");
        var pageResponse = await _client.SendAsync(page, TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.OK, pageResponse.StatusCode);
    }

    /// <summary>
    /// An account that has not accepted the service terms gets its new password but is not signed in (login
    /// refuses it as well): the "password changed" page is shown and no session cookie is issued.
    /// </summary>
    [Fact]
    public async Task ResetPassword_Post_ValidToken_TermsNotAccepted_ShowsTheCompletionPage_WithoutSigningIn()
    {
        string email = UniqueEmail();
        string rawToken = GuidToken.Generate(DateTime.UtcNow);
        await SeedAccountAsync(email, emailConfirmed: true, resetPasswordToken: rawToken, agreedServiceTerms: false);

        var response = await PostFormAsync("/Account/ResetPassword", new Dictionary<string, string>
        {
            ["ResetPasswordToken"] = EncryptToken(rawToken),
            ["Password"] = "BrandNewPassword1!"
        });
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Null(ExtractCookieValue(response, SessionCookieName));
        Assert.Contains("Your account password has been successfully changed", html);
    }

    /// <summary>Reset password post unknown token returns fail to reset.</summary>
    [Fact]
    public async Task ResetPassword_Post_UnknownToken_ReturnsFailToReset()
    {
        using var scope = factory.Services.CreateScope();
        var rsa = scope.ServiceProvider.GetRequiredService<IRsaService>();
        string encryptedToken = rsa.Encrypt(GuidToken.Generate(DateTime.UtcNow)); // well-formed but not stored on any account

        var response = await PostFormAsync("/Account/ResetPassword", new Dictionary<string, string>
        {
            ["ResetPasswordToken"] = encryptedToken,
            ["Password"] = "WontBeApplied1!"
        });
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("The authentication token is invalid.", html);
    }

    /// <summary>
    /// Regression: a new password over 72 UTF-8 bytes (here 3 + 25 Hangul = 78 bytes, but well under 100
    /// characters) used to be reported as "The authentication token is invalid." — although nothing was wrong
    /// with the token. The form must come back (with the token) and say what is wrong with the password, and
    /// the reset token must stay usable.
    /// </summary>
    [Fact]
    public async Task ResetPassword_Post_PasswordOver72Bytes_ReShowsTheFormAndKeepsTheToken()
    {
        string email = UniqueEmail();
        string rawToken = GuidToken.Generate(DateTime.UtcNow);
        await SeedAccountAsync(email, emailConfirmed: true, resetPasswordToken: rawToken);

        using var scope = factory.Services.CreateScope();
        var rsa = scope.ServiceProvider.GetRequiredService<IRsaService>();
        string encryptedToken = rsa.Encrypt(rawToken);

        var response = await PostFormAsync("/Account/ResetPassword", new Dictionary<string, string>
        {
            ["ResetPasswordToken"] = encryptedToken,
            ["Password"] = "Aa1" + new string('가', 25)
        });
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("resetPasswordForm", html);
        Assert.DoesNotContain("The authentication token is invalid.", html);
        Assert.Contains("at most 72 bytes", html);

        using var verifyScope = factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var account = db.Accounts.Single(a => a.Email == email);
        Assert.Equal(rawToken, account.ResetPasswordToken); // still usable
    }

    /// <summary>
    /// Regression: a registration the service rejects (here a reserved nickname) sent no mail, so it
    /// must not burn the per-address e-mail cooldown — the corrected form, submitted straight away,
    /// has to go through instead of being told to "wait a moment before requesting another email".
    /// </summary>
    [Fact]
    public async Task Register_RejectedByService_DoesNotConsumeTheEmailCooldown()
    {
        string email = UniqueEmail();

        var rejected = await PostFormAsync("/Account/Register", new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = "NewPassword1!",
            ["Nickname"] = "Admin",
            ["AgreedServiceTerms"] = "true",
            ["TimeZoneIanaId"] = "UTC"
        });
        Assert.Equal(System.Net.HttpStatusCode.OK, rejected.StatusCode);

        var accepted = await PostFormAsync("/Account/Register", new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = "NewPassword1!",
            ["Nickname"] = "RegRetry_" + Guid.NewGuid().ToString("N")[..8],
            ["AgreedServiceTerms"] = "true",
            ["TimeZoneIanaId"] = "UTC"
        });
        Assert.Equal(System.Net.HttpStatusCode.OK, accepted.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(db.Accounts.Any(a => a.Email == email), "the corrected registration was throttled");
        var publisher = scope.ServiceProvider.GetRequiredService<FakeEmailPublisher>();
        Assert.Contains(publisher.PublishedMessages, m => m.ToEmail == email);
    }

    // -- Refusals that re-render the form ---------------------------------------------------------

    /// <summary>Posts <paramref name="fields"/> to <c>/Account/Login</c> with a fresh antiforgery token, optionally as <paramref name="userAgent"/>.</summary>
    private async Task<HttpResponseMessage> PostLoginAsync(Dictionary<string, string> fields, string? userAgent = null)
    {
        var (cookie, token) = await GetAntiForgeryAsync("/Account/Login");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Account/Login");
        request.Headers.Add("Cookie", $"{AntiForgeryCookieName}={cookie}");
        if (userAgent != null) request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        fields["__RequestVerificationToken"] = token;
        request.Content = new FormUrlEncodedContent(fields);
        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>Internet Explorer is refused before credentials are even looked at — even correct ones.</summary>
    [Theory]
    [InlineData("Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.2; Trident/6.0)")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Trident/7.0; rv:11.0) like Gecko")]
    public async Task Login_Post_FromInternetExplorer_IsBlocked_EvenWithCorrectCredentials(string userAgent)
    {
        string email = UniqueEmail();
        await SeedAccountAsync(email, emailConfirmed: true);

        var response = await PostLoginAsync(new Dictionary<string, string> { ["Email"] = email, ["Password"] = "OldPassword1!" }, userAgent);
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode); // a successful login redirects
        Assert.Contains("does not support Internet Explorer", html);
    }

    /// <summary>
    /// Regression test: a mobile keyboard's autocomplete adds a space after the address (and some add
    /// one before). The address is matched the way registration stored it — trimmed and lower-cased —
    /// so the owner is signed in instead of being told the email or password is wrong.
    /// </summary>
    [Theory]
    [InlineData("{0} ")]
    [InlineData("  {0}")]
    [InlineData(" {0}\t")]
    public async Task Login_Post_SignsIn_WhenTheEmailHasSurroundingWhitespace(string format)
    {
        string email = UniqueEmail();
        await SeedAccountAsync(email, emailConfirmed: true);

        var response = await PostLoginAsync(new Dictionary<string, string> { ["Email"] = string.Format(format, email.ToUpperInvariant()), ["Password"] = "OldPassword1!" });

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode); // a successful login redirects
    }

    /// <summary>A login the service refuses (wrong password) re-renders the form with a message instead of redirecting.</summary>
    [Fact]
    public async Task Login_Post_WithAWrongPassword_ReShowsTheFormWithAnError()
    {
        string email = UniqueEmail();
        await SeedAccountAsync(email, emailConfirmed: true);

        var response = await PostLoginAsync(new Dictionary<string, string> { ["Email"] = email, ["Password"] = "NotThePassword9!" });
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("loginForm", html);
    }

    /// <summary>
    /// Asking for the confirmation mail again (empty password) for an address that has no account, or whose account is
    /// already confirmed, sends nothing, says why, and releases the resend cool-down so the visitor can retry at once.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Register_ResendRequest_TheServiceRefuses_SendsNothing_AndReleasesTheCooldown(bool accountExists)
    {
        string email = UniqueEmail();
        if (accountExists) await SeedAccountAsync(email, emailConfirmed: true);

        var first = await PostFormAsync("/Account/Register", Fields());
        var second = await PostFormAsync("/Account/Register", Fields());
        string secondHtml = await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, second.StatusCode); // not answered by the cool-down (which is 429)
        Assert.Contains("registerForm", secondHtml);
        using var scope = factory.Services.CreateScope();
        Assert.DoesNotContain(scope.ServiceProvider.GetRequiredService<FakeEmailPublisher>().PublishedMessages, m => m.ToEmail == email);
        return;

        Dictionary<string, string> Fields() => new()
        {
            ["Email"] = email,
            ["Password"] = "",
            ["Nickname"] = "",
            ["AgreedServiceTerms"] = "false",
            ["TimeZoneIanaId"] = ""
        };
    }

    /// <summary>A reset with a live token but a password the policy refuses keeps the form (and the token) instead of consuming it.</summary>
    [Fact]
    public async Task ResetPassword_Post_PasswordTheServiceRefuses_ReShowsTheFormAndKeepsTheToken()
    {
        string email = UniqueEmail();
        string rawToken = GuidToken.Generate(DateTime.UtcNow);
        await SeedAccountAsync(email, emailConfirmed: true, resetPasswordToken: rawToken);
        using var scope = factory.Services.CreateScope();
        string encryptedToken = scope.ServiceProvider.GetRequiredService<IRsaService>().Encrypt(rawToken);

        var response = await PostFormAsync("/Account/ResetPassword", new Dictionary<string, string>
        {
            ["ResetPasswordToken"] = encryptedToken, ["Password"] = "weak"
        });
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("resetPasswordForm", html);
        Assert.DoesNotContain("The authentication token is invalid.", html);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(rawToken, db.Accounts.Single(a => a.Email == email).ResetPasswordToken);
    }
}
