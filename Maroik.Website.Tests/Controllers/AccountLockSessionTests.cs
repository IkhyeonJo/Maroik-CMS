using Maroik.Core.Domain.Account;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// What a lock does to a session that is already signed in. A lock from failed logins only refuses new logins —
/// otherwise anyone who knows an address could throw its owner out of the site by guessing wrong on purpose. An
/// administrator's lock ends the account's sessions as well.
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

    /// <summary>Posts the login form for <paramref name="email"/> with <paramref name="password"/>, from a browser with no session.</summary>
    private async Task<HttpResponseMessage> PostLoginAsync(string email, string password)
    {
        var anonymous = await AuthenticatedSessionHelper.AnonymousAsync(_client, TestContext.Current.CancellationToken);
        var form = new MultipartFormDataContent
        {
            { new StringContent(email), "Email" },
            { new StringContent(password), "Password" }
        };
        using var request = anonymous.BuildFormPostRequest("/Account/Login", form);
        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>The stored <c>Locked</c> flag of <paramref name="email"/>.</summary>
    private bool IsLocked(string email)
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Accounts.Single(a => a.Email == email).Locked;
    }

    /// <summary>
    /// Someone else guesses wrong until the account locks: the owner's open session keeps working, and only a new
    /// login (even with the right password) is refused.
    /// </summary>
    [Fact]
    public async Task AFailedLoginLock_KeepsTheOpenSession_AndOnlyRefusesNewLogins()
    {
        string email = UniqueEmail();
        AuthenticatedSession owner = await SignInAsync(email);

        for (int i = 0; i < MaxLoginAttempt; i++)
            await PostLoginAsync(email, "WrongGuess1!");

        Assert.True(IsLocked(email));
        Assert.Equal(System.Net.HttpStatusCode.OK, (await OpenIncomePageAsync(owner)).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, (await PostLoginAsync(email, Password)).StatusCode); // refused: the form again, not a redirect
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
