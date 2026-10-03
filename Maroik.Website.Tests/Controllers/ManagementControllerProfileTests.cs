using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Account;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// Integration tests for the Profile action group of <c>ManagementController</c> —
/// <c>UpdateProfileTimeZone</c>/<c>UpdateProfilePassword</c>/<c>UpdateProfileAvatar</c> —
/// previously entirely uncovered.
///
/// <c>UpdateProfileAvatar</c>'s success path is not covered here: it calls
/// <c>ProfileService.UploadAndUpdateAvatarAsync</c>, which stores the image through the
/// file-storage service (<c>ServerSetting.FileStorageBaseUrl</c>) — not running in this test
/// environment, so a real upload always fails at that step. Only the controller-level validation
/// branches that return before the service is reached (missing file, oversized file, disallowed
/// extension) are exercised.
/// </summary>
[Collection("Website Integration")]
public class ManagementControllerProfileTests(MaroikWebApplicationFactory factory)
{
    /// <summary>Client for the shared test host; redirects are not followed so they can be asserted.</summary>
    private readonly HttpClient _client = factory.CreateTestClient(followRedirects: false);
    
    /// <summary>Seeds (if missing) a User account for <paramref name="email"/> and signs in as it.</summary>
    private Task<AuthenticatedSession> LoginAsUserAsync(string email = "management-profile-user@test.com") =>
        AuthenticatedSessionHelper.LoginAsync(factory, _client, email, "UserPassword1!", Role.User, TestContext.Current.CancellationToken);

    // -- Profile: GET -----------------------------------------------------------------

    /// <summary>Profile get logged in user returns200.</summary>
    [Fact]
    public async Task Profile_Get_LoggedInUser_Returns200()
    {
        var session = await LoginAsUserAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Management/Profile");
        request.Headers.Add("Cookie", session.CookieHeader);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// The avatar picker's <c>accept</c> filter is rendered from <c>ImageUploadPolicy</c> (the single source of
    /// truth the server validates against), not a hand-typed list that can drift from it.
    /// </summary>
    [Fact]
    public async Task Profile_Get_AvatarFileInput_AcceptsExactlyTheImagePolicyContentTypes()
    {
        var session = await LoginAsUserAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Management/Profile");
        request.Headers.Add("Cookie", session.CookieHeader);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        string expected = string.Join(", ", Core.Domain.Media.ImageUploadPolicy.AllowedContentTypes);
        Assert.Contains("id=\"ProfileAvatarFiles\"", html);
        Assert.Matches($"id=\"ProfileAvatarFiles\"[^>]*accept=\"{System.Text.RegularExpressions.Regex.Escape(expected)}\"", html);
    }

    // -- UpdateProfileTimeZone ----------------------------------------------------------

    /// <summary>Update profile time zone valid id persists change.</summary>
    [Fact]
    public async Task UpdateProfileTimeZone_ValidId_PersistsChange()
    {
        var session = await LoginAsUserAsync("management-profile-tz@test.com");
        using var request = session.BuildFormPostRequest("/Management/UpdateProfileTimeZone",
            new MultipartFormDataContent { { new StringContent("Asia/Seoul"), "TimeZoneIanaId" } });

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var account = db.Accounts.Single(a => a.Email == "management-profile-tz@test.com");
        Assert.Equal("Asia/Seoul", account.TimeZoneIanaId);
    }

    /// <summary>Update profile time zone invalid time zone leaves account unchanged.</summary>
    [Fact]
    public async Task UpdateProfileTimeZone_InvalidTimeZone_LeavesAccountUnchanged()
    {
        var session = await LoginAsUserAsync("management-profile-tz-bad@test.com");
        using var request = session.BuildFormPostRequest("/Management/UpdateProfileTimeZone",
            new MultipartFormDataContent { { new StringContent("Not/ARealZone"), "TimeZoneIanaId" } });

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode); // redirects regardless; failure is logged server-side

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var account = db.Accounts.Single(a => a.Email == "management-profile-tz-bad@test.com");
        Assert.Equal("UTC", account.TimeZoneIanaId); // unchanged from seed default
    }

    // -- UpdateProfilePassword ----------------------------------------------------------

    /// <summary>Update profile password correct current password changes password.</summary>
    [Fact]
    public async Task UpdateProfilePassword_CorrectCurrentPassword_ChangesPassword()
    {
        var session = await LoginAsUserAsync("management-profile-pw@test.com");
        using var request = session.BuildJsonPostRequest("/Management/UpdateProfilePassword", new
        {
            Nickname = "unused",
            Password = "UserPassword1!",
            NewPassword = "BrandNewPassword1!",
            TimeZoneIanaId = "unused"
        });

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var account = db.Accounts.Single(a => a.Email == "management-profile-pw@test.com");
        Assert.True(passwordService.VerifyPassword("BrandNewPassword1!", account.HashedPassword));
    }

    /// <summary>
    /// Changing the password signs every session of the account out — the one that made the change
    /// included — and the response tells the user to sign in again (the client then goes to the
    /// sign-in page). The next request with the same session cookie is no longer authenticated.
    /// </summary>
    [Fact]
    public async Task UpdateProfilePassword_Success_EndsTheCurrentSessionToo()
    {
        var session = await LoginAsUserAsync("management-profile-pw-signout@test.com");
        using var change = session.BuildJsonPostRequest("/Management/UpdateProfilePassword", new
        {
            Nickname = "unused",
            Password = "UserPassword1!",
            NewPassword = "BrandNewPassword1!",
            TimeZoneIanaId = "unused"
        });

        var changeResponse = await _client.SendAsync(change, TestContext.Current.CancellationToken);
        string json = await changeResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);
        Assert.Contains("sign in again", json);

        using var profile = new HttpRequestMessage(HttpMethod.Get, "/Management/Profile");
        profile.Headers.Add("Cookie", session.CookieHeader);
        var profileResponse = await _client.SendAsync(profile, TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, profileResponse.StatusCode);
    }

    /// <summary>
    /// Someone else's failed logins lock the account while its owner is signed in (that lock keeps the session). The
    /// owner changes the password, which ends the session and asks for a new sign-in: the change lifts the lock, so that
    /// sign-in with the new password succeeds instead of being refused as locked.
    /// </summary>
    [Fact]
    public async Task UpdateProfilePassword_OnAnAccountLockedByFailedLogins_LiftsTheLock_SoTheNewSignInSucceeds()
    {
        const string email = "management-profile-pw-locked@test.com";
        var ct = TestContext.Current.CancellationToken;
        var session = await LoginAsUserAsync(email);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Accounts.Where(a => a.Email == email).ExecuteUpdateAsync(s => s
                .SetProperty(a => a.Locked, true)
                .SetProperty(a => a.LoginAttempt, 3)
                .SetProperty(a => a.Message, "This account is locked"), ct);
        }

        using var change = session.BuildJsonPostRequest("/Management/UpdateProfilePassword", new
        {
            Nickname = "unused",
            Password = "UserPassword1!",
            NewPassword = "BrandNewPassword1!",
            TimeZoneIanaId = "unused"
        });
        string json = await (await _client.SendAsync(change, ct)).Content.ReadAsStringAsync(ct);

        Assert.Contains("\"result\":true", json);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var account = db.Accounts.AsNoTracking().Single(a => a.Email == email);
            Assert.False(account.Locked);
            Assert.Equal(0, account.LoginAttempt);
            Assert.Null(account.Message);
        }

        var signedInAgain = await AuthenticatedSessionHelper.LoginAsync(factory, _client, email, "BrandNewPassword1!", Role.User, ct);
        using var profile = new HttpRequestMessage(HttpMethod.Get, "/Management/Profile");
        profile.Headers.Add("Cookie", signedInAgain.CookieHeader);
        Assert.Equal(System.Net.HttpStatusCode.OK, (await _client.SendAsync(profile, ct)).StatusCode);
    }

    /// <summary>
    /// Regression: the CURRENT password only has to match what is stored — it is not held to the
    /// complexity rule. An account whose password predates the rule (here a short, single-class one)
    /// must still be able to change it.
    /// </summary>
    [Fact]
    public async Task UpdateProfilePassword_LegacyWeakCurrentPassword_ChangesPassword()
    {
        const string email = "management-profile-pw-legacy@test.com";
        var session = await AuthenticatedSessionHelper.LoginAsync(
            factory, _client, email, "weak", Role.User, TestContext.Current.CancellationToken);
        using var request = session.BuildJsonPostRequest("/Management/UpdateProfilePassword", new
        {
            Nickname = "unused",
            Password = "weak",
            NewPassword = "BrandNewPassword1!",
            TimeZoneIanaId = "unused"
        });

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var account = db.Accounts.Single(a => a.Email == email);
        Assert.True(passwordService.VerifyPassword("BrandNewPassword1!", account.HashedPassword));
    }

    /// <summary>A new password over 72 UTF-8 bytes is refused (BCrypt would silently truncate it) and nothing changes.</summary>
    [Fact]
    public async Task UpdateProfilePassword_NewPasswordOver72Bytes_ReturnsFailureResult()
    {
        var session = await LoginAsUserAsync("management-profile-pw-toolong@test.com");
        using var request = session.BuildJsonPostRequest("/Management/UpdateProfilePassword", new
        {
            Nickname = "unused",
            Password = "UserPassword1!",
            NewPassword = "Aa1" + new string('가', 25),
            TimeZoneIanaId = "unused"
        });

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var account = db.Accounts.Single(a => a.Email == "management-profile-pw-toolong@test.com");
        Assert.True(passwordService.VerifyPassword("UserPassword1!", account.HashedPassword)); // unchanged
    }

    /// <summary>Update profile password wrong current password returns failure result.</summary>
    [Fact]
    public async Task UpdateProfilePassword_WrongCurrentPassword_ReturnsFailureResult()
    {
        var session = await LoginAsUserAsync("management-profile-pw-wrong@test.com");
        using var request = session.BuildJsonPostRequest("/Management/UpdateProfilePassword", new
        {
            Nickname = "unused",
            Password = "TotallyWrongPassword1!",
            NewPassword = "BrandNewPassword1!",
            TimeZoneIanaId = "unused"
        });

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var account = db.Accounts.Single(a => a.Email == "management-profile-pw-wrong@test.com");
        Assert.True(passwordService.VerifyPassword("UserPassword1!", account.HashedPassword)); // unchanged
    }

    // -- UpdateProfileAvatar: validation only (see class doc comment) -------------------

    /// <summary>Update profile avatar no file attached returns failure result.</summary>
    [Fact]
    public async Task UpdateProfileAvatar_NoFileAttached_ReturnsFailureResult()
    {
        var session = await LoginAsUserAsync("management-profile-avatar-nofile@test.com");
        // A real client always submits at least the other profile fields alongside the file
        // input, so a literally empty multipart body isn't representative — one harmless field
        // keeps the request well-formed while still attaching no file.
        var form = new MultipartFormDataContent { { new StringContent("unused"), "Nickname" } };
        using var request = session.BuildFormPostRequest("/Management/UpdateProfileAvatar", form);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"result\":false", json);
    }

    /// <summary>Update profile avatar disallowed extension returns failure result.</summary>
    [Fact]
    public async Task UpdateProfileAvatar_DisallowedExtension_ReturnsFailureResult()
    {
        var session = await LoginAsUserAsync("management-profile-avatar-badext@test.com");
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([1, 2, 3, 4]);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        form.Add(fileContent, "ProfileAvatarFiles", "notanimage.txt");
        using var request = session.BuildFormPostRequest("/Management/UpdateProfileAvatar", form);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"result\":false", json);
    }
}
