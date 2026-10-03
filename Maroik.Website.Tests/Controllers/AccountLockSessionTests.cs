using Maroik.Core.Domain.Account;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// What failed logins and a lock do to a session that is already signed in. Failed logins never lock the account:
/// they hold new logins off for a while (LoginThrottlePolicy) and leave open sessions alone — otherwise anyone who
/// knows an address could throw its owner out of the site by guessing wrong on purpose. An administrator's lock
/// ends the account's sessions.
/// </summary>
[Collection("Website Integration")]
public class AccountLockSessionTests(MaroikWebApplicationFactory factory)
{
    /// <summary>Client for the shared test host; redirects are not followed so they can be asserted.</summary>
    private readonly HttpClient _client = factory.CreateTestClient(followRedirects: false);

    /// <summary>The user's password in these tests.</summary>
    private const string Password = "UserPassword1!";

    /// <summary>The failed-login threshold the test host is configured with (ServerSetting:MaxLoginAttempt).</summary>
    private const int MaxLoginAttempt = 5;

    /// <summary>A fresh address for this test.</summary>
    private static string UniqueEmail() => $"lock-session-{Guid.NewGuid():N}@test.com";

    /// <summary>Signs in a fresh account with <paramref name="email"/> and <paramref name="role"/>.</summary>
    private Task<AuthenticatedSession> SignInAsync(string email, string role = Role.User) =>
        AuthenticatedSessionHelper.LoginAsync(factory, _client, email, Password, role, TestContext.Current.CancellationToken);

    /// <summary>Opens a signed-in-only page with <paramref name="session"/> and returns the response.</summary>
    private async Task<HttpResponseMessage> OpenIncomePageAsync(AuthenticatedSession session)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/AccountBook/Income");
        request.Headers.Add("Cookie", session.CookieHeader);
        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Posts the login form for <paramref name="email"/> with <paramref name="password"/>, from a browser with no
    /// session (asking for <paramref name="culture"/> when given).
    /// </summary>
    private async Task<HttpResponseMessage> PostLoginAsync(string email, string password, string? culture = null)
    {
        var anonymous = await AuthenticatedSessionHelper.AnonymousAsync(_client, TestContext.Current.CancellationToken);
        var form = new MultipartFormDataContent
        {
            { new StringContent(email), "Email" },
            { new StringContent(password), "Password" }
        };
        using var request = anonymous.BuildFormPostRequest("/Account/Login", form);
        if (culture != null) request.Headers.AcceptLanguage.ParseAdd(culture);
        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>The stored <c>Locked</c> flag and login wait of <paramref name="email"/>.</summary>
    private (bool Locked, DateTime? BlockedUntil) StoredState(string email)
    {
        using var scope = factory.Services.CreateScope();
        var account = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Accounts.Single(a => a.Email == email);
        return (account.Locked, account.LoginBlockedUntil);
    }

    /// <summary>
    /// Someone else guesses wrong up to the configured step: the account is not locked but its new logins are held
    /// off for a while, the owner's open session keeps working, and a new login (even with the right password) is
    /// refused with the "too many attempts" message.
    /// </summary>
    [Fact]
    public async Task FailedLogins_HoldNewLoginsOff_AndKeepTheOpenSession()
    {
        string email = UniqueEmail();
        AuthenticatedSession owner = await SignInAsync(email);

        for (int i = 0; i < MaxLoginAttempt; i++)
            await PostLoginAsync(email, "WrongGuess1!");

        (bool locked, DateTime? blockedUntil) = StoredState(email);
        Assert.False(locked);
        Assert.NotNull(blockedUntil);
        Assert.Equal(System.Net.HttpStatusCode.OK, (await OpenIncomePageAsync(owner)).StatusCode);
        var refused = await PostLoginAsync(email, Password);
        Assert.Equal(System.Net.HttpStatusCode.OK, refused.StatusCode); // refused: the form again, not a redirect
        Assert.Contains("Too many failed sign-in attempts. Please try again later or reset your password.",
            await refused.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>A Korean browser is told the same in Korean.</summary>
    [Fact]
    public async Task FailedLogins_TheHeldOffRefusal_IsShownInKorean_ToAKoreanBrowser()
    {
        string email = UniqueEmail();
        await SignInAsync(email);
        for (int i = 0; i < MaxLoginAttempt; i++)
            await PostLoginAsync(email, "WrongGuess1!");

        var refused = await PostLoginAsync(email, Password, culture: "ko-KR");

        // Razor writes non-ASCII text as character references (&#xB85C;...), so compare the decoded page.
        Assert.Contains("로그인 실패가 너무 많습니다. 잠시 후 다시 시도하거나 비밀번호를 재설정해 주세요.",
            System.Net.WebUtility.HtmlDecode(await refused.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
    }

    /// <summary>The alert mails queued so far to <paramref name="email"/>.</summary>
    private List<Maroik.Core.Contract.Misc.Messaging.SendEmailMessage> AlertsTo(string email)
    {
        using var scope = factory.Services.CreateScope();
        return [.. scope.ServiceProvider.GetRequiredService<FakeEmailPublisher>().PublishedMessages.Where(m => m.ToEmail == email)];
    }

    /// <summary>
    /// The wrong password that starts the account's first wait mails its owner one alert, linking to the
    /// forgot-password page; the failures that follow mail nothing more.
    /// </summary>
    [Fact]
    public async Task FailedLogins_MailTheOwnerOneAlert_LinkingToTheForgotPasswordPage()
    {
        string email = UniqueEmail();
        await SignInAsync(email);

        for (int i = 0; i < MaxLoginAttempt * 2; i++)
            await PostLoginAsync(email, "WrongGuess1!");

        var alert = Assert.Single(AlertsTo(email));
        Assert.Equal("Maroik sign-in alert", alert.Subject);
        Assert.Contains("Several sign-ins to your account failed", alert.Body);
        Assert.Contains("/Account/ForgotPassword", alert.Body);
    }

    /// <summary>The alert is written in the language of the browser whose failure started the wait.</summary>
    [Fact]
    public async Task FailedLogins_TheAlert_IsInKorean_WhenAKoreanBrowserStartedTheWait()
    {
        string email = UniqueEmail();
        await SignInAsync(email);

        for (int i = 0; i < MaxLoginAttempt; i++)
            await PostLoginAsync(email, "WrongGuess1!", culture: "ko-KR");

        var alert = Assert.Single(AlertsTo(email));
        Assert.Equal("Maroik 로그인 알림", alert.Subject);
        Assert.Contains("계정 로그인이 여러 번 실패했습니다", System.Net.WebUtility.HtmlDecode(alert.Body));
    }

    /// <summary>An administrator locking the account ends its open session: the next request goes to the login page.</summary>
    [Fact]
    public async Task AnAdminLock_EndsTheOpenSession()
    {
        string email = UniqueEmail();
        AuthenticatedSession owner = await SignInAsync(email);
        AuthenticatedSession admin = await SignInAsync(UniqueEmail(), Role.Admin);
        using var lockRequest = admin.BuildJsonPostRequest("/Management/UpdateAccount", new
        {
            Email = email,
            Password = "",
            Role = Role.User,
            TimeZoneIanaId = "UTC",
            Locked = true,
            EmailConfirmed = true,
            AgreedServiceTerms = true,
            Message = "",
            Deleted = false
        });
        Assert.Contains("\"result\":true", await (await _client.SendAsync(lockRequest, TestContext.Current.CancellationToken))
            .Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var response = await OpenIncomePageAsync(owner);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.OriginalString);
    }
}
