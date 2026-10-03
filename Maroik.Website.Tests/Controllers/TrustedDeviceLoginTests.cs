using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Helpers;
using Maroik.Core.Domain.Account;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Website.Services;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// Trusted devices end to end: a successful sign-in (or the sign-in after a password reset) leaves a trusted-device
/// cookie in the browser, and that browser keeps signing in while failed logins made elsewhere hold the account's
/// other logins off.
/// </summary>
[Collection("Website Integration")]
public class TrustedDeviceLoginTests(MaroikWebApplicationFactory factory)
{
    /// <summary>Client for the shared test host; redirects are not followed so they can be asserted.</summary>
    private readonly HttpClient _client = factory.CreateTestClient(followRedirects: false);

    /// <summary>The account's password in these tests.</summary>
    private const string Password = "UserPassword1!";

    /// <summary>The failed-login step the test host is configured with (ServerSetting:MaxLoginAttempt).</summary>
    private const int MaxLoginAttempt = 5;

    /// <summary>A fresh address for this test.</summary>
    private static string UniqueEmail() => $"trusted-device-{Guid.NewGuid():N}@test.com";

    /// <summary>Seeds a confirmed User account <paramref name="email"/> with <see cref="Password"/> (and <paramref name="resetToken"/>).</summary>
    private void SeedAccount(string email, string? resetToken = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Accounts.Add(new Maroik.Core.PostgreSQL.Models.Account
        {
            Email = email,
            HashedPassword = scope.ServiceProvider.GetRequiredService<IPasswordService>().HashPassword(Password),
            Nickname = "Trusted_" + Guid.NewGuid().ToString("N")[..12],
            AvatarImagePath = "/upload/Management/Profile/default-avatar.jpg",
            Role = Role.User,
            TimeZoneIanaId = "UTC",
            EmailConfirmed = true,
            AgreedServiceTerms = true,
            ResetPasswordToken = resetToken,
            Created = DateTime.UtcNow,
            Updated = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    /// <summary>
    /// Posts <paramref name="fields"/> to <paramref name="url"/> as a signed-out browser, carrying
    /// <paramref name="deviceCookie"/> (a <c>name=value</c> pair) when given.
    /// </summary>
    private async Task<HttpResponseMessage> PostAsync(string url, Dictionary<string, string> fields, string? deviceCookie = null)
    {
        var anonymous = await AuthenticatedSessionHelper.AnonymousAsync(_client, TestContext.Current.CancellationToken);
        var form = new MultipartFormDataContent();
        foreach (var (name, value) in fields)
            form.Add(new StringContent(value), name);
        using var request = anonymous.BuildFormPostRequest(url, form);
        if (deviceCookie != null)
        {
            string cookies = string.Join("; ", request.Headers.GetValues("Cookie"));
            request.Headers.Remove("Cookie");
            request.Headers.Add("Cookie", $"{cookies}; {deviceCookie}");
        }
        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>Signs in as <paramref name="email"/> with <paramref name="password"/> (from a browser holding <paramref name="deviceCookie"/>).</summary>
    private Task<HttpResponseMessage> SignInAsync(string email, string password, string? deviceCookie = null) =>
        PostAsync("/Account/Login", new() { ["Email"] = email, ["Password"] = password }, deviceCookie);

    /// <summary>The trusted-device cookie <paramref name="response"/> sets, as a <c>name=value</c> pair, or <see langword="null"/>.</summary>
    private static string? DeviceCookieOf(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var headers)
            ? headers.Select(h => h.Split(';')[0]).FirstOrDefault(c => c.StartsWith(TrustedDeviceCookie.CookieName + "=", StringComparison.Ordinal))
            : null;

    /// <summary>A successful sign-in leaves a trusted-device cookie; a failed one does not.</summary>
    [Fact]
    public async Task SignIn_IssuesATrustedDeviceCookie_OnlyWhenItSucceeds()
    {
        string email = UniqueEmail();
        SeedAccount(email);

        var failed = await SignInAsync(email, "WrongGuess1!");
        var succeeded = await SignInAsync(email, Password);

        Assert.Null(DeviceCookieOf(failed));
        Assert.Equal(System.Net.HttpStatusCode.Redirect, succeeded.StatusCode);
        Assert.NotNull(DeviceCookieOf(succeeded));
    }

    /// <summary>
    /// After someone else's wrong guesses hold the account's logins off, the owner's browser with its trusted-device
    /// cookie still signs in; a browser without one is refused.
    /// </summary>
    [Fact]
    public async Task ATrustedDevice_SignsIn_WhileFailedLoginsHoldOtherLoginsOff()
    {
        string email = UniqueEmail();
        SeedAccount(email);
        string deviceCookie = DeviceCookieOf(await SignInAsync(email, Password))!;
        for (int i = 0; i < MaxLoginAttempt; i++)
            await SignInAsync(email, "WrongGuess1!");

        var withoutCookie = await SignInAsync(email, Password);
        var withCookie = await SignInAsync(email, Password, deviceCookie);

        Assert.Equal(System.Net.HttpStatusCode.OK, withoutCookie.StatusCode); // refused: the form again
        Assert.Equal(System.Net.HttpStatusCode.Redirect, withCookie.StatusCode);
    }

    /// <summary>The sign-in that follows a password reset leaves a trusted-device cookie too.</summary>
    [Fact]
    public async Task ResetPassword_SigningIn_IssuesATrustedDeviceCookie()
    {
        string email = UniqueEmail();
        string rawToken = GuidToken.Generate(DateTime.UtcNow);
        SeedAccount(email, resetToken: rawToken);
        string encrypted;
        using (var scope = factory.Services.CreateScope())
            encrypted = scope.ServiceProvider.GetRequiredService<IRsaService>().Encrypt(rawToken);

        var response = await PostAsync("/Account/ResetPassword", new() { ["ResetPasswordToken"] = encrypted, ["Password"] = "BrandNewPassword1!" });

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(DeviceCookieOf(response));
    }
}
