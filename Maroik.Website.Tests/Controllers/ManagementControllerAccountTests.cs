using Maroik.Core.Domain.Account;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// Integration tests for the Account CRUD action group of <c>ManagementController</c>
/// (create/read/update/delete, all <c>[RequiredHttpPostAccess(Role = Role.Admin)]</c>-gated).
/// Drives a real authenticated session via <see cref="AuthenticatedSessionHelper"/> so the
/// inline validation and role-gating logic in the controller is exercised end-to-end.
///
/// Runs against a real PostgreSQL instance (via <see cref="MaroikWebApplicationFactory"/>'s
/// Testcontainers setup), so <c>CreateAccount</c>'s reliance on the <c>AvatarImagePath</c>
/// column's <c>HasDefaultValueSql</c> — a SQL default the EF Core InMemory provider cannot
/// evaluate — works exactly as in production and is exercised directly below.
/// </summary>
[Collection("Website Integration")]
public class ManagementControllerAccountTests(MaroikWebApplicationFactory factory)
{
    /// <summary>Client for the shared test host; redirects are not followed so they can be asserted.</summary>
    private readonly HttpClient _client = factory.CreateTestClient(followRedirects: false);
    
    /// <summary>Seeds (if missing) an Admin account for <paramref name="email"/> and signs in as it.</summary>
    private Task<AuthenticatedSession> LoginAsAdminAsync(string email = "mgmt-admin@test.com") =>
        AuthenticatedSessionHelper.LoginAsync(factory, _client, email, "AdminPassword1!", Role.Admin, TestContext.Current.CancellationToken);

    /// <summary>Seeds (if missing) a User account for <paramref name="email"/> and signs in as it.</summary>
    private Task<AuthenticatedSession> LoginAsUserAsync(string email = "mgmt-user@test.com") =>
        AuthenticatedSessionHelper.LoginAsync(factory, _client, email, "UserPassword1!", Role.User, TestContext.Current.CancellationToken);

    /// <summary>Inserts a User account for <paramref name="email"/> directly into the database, unless it already exists.</summary>
    private void SeedAccount(string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (db.Accounts.Any(a => a.Email == email)) return;
        db.Accounts.Add(new Maroik.Core.PostgreSQL.Models.Account
        {
            Email = email,
            HashedPassword = "irrelevant-hash",
            Nickname = $"Seed-{Guid.NewGuid():N}",
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
        db.SaveChanges();
    }

    // Nickname carries a real unique constraint under Postgres, so it must vary per email
    // rather than being a fixed literal shared by every call site.
    private static object ValidAccountPayload(string email) => new
    {
        Email = email,
        Password = "NewAccountPw1!",
        Nickname = "Newcomer_" + email.Replace("@", "_").Replace(".", "_"),
        AvatarImagePath = "",
        Role = Role.User,
        TimeZoneIanaId = "UTC",
        Locked = false,
        LoginAttempt = 0,
        EmailConfirmed = true,
        AgreedServiceTerms = true,
        RegistrationToken = "",
        ResetPasswordToken = "",
        Message = "",
        Deleted = false
    };

    // -- CreateAccount: role gating --------------------------------------------

    /// <summary>Create account user session is forbidden.</summary>
    [Fact]
    public async Task CreateAccount_UserSession_IsForbidden()
    {
        var session = await LoginAsUserAsync();
        using var request = session.BuildJsonPostRequest("/Management/CreateAccount", ValidAccountPayload("blocked@test.com"));

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.NotEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    // -- CreateAccount: inline validation ---------------------------------------

    /// <summary>Create account invalid email format returns specific error.</summary>
    [Fact]
    public async Task CreateAccount_InvalidEmailFormat_ReturnsSpecificError()
    {
        var session = await LoginAsAdminAsync();
        using var request = session.BuildJsonPostRequest("/Management/CreateAccount", ValidAccountPayload("not-an-email"));

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = System.Text.Json.JsonDocument.Parse(json);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        // Email.Create (Core) message, not a controller-local string. Asserts the full exact text —
        // not just a substring — so a regression that stops substituting the "{0}" template argument
        // (see LocalizedHtmlStringExtensionsTests) would be caught here too.
        Assert.Equal("'not-an-email' is not a valid email address.", doc.RootElement.GetProperty("error").GetString());
    }

    /// <summary>Create account weak password returns password policy error.</summary>
    [Fact]
    public async Task CreateAccount_WeakPassword_ReturnsPasswordPolicyError()
    {
        var session = await LoginAsAdminAsync();
        var payload = new
        {
            Email = "weakpass@test.com",
            Password = "weak",
            Nickname = "Weak",
            AvatarImagePath = "",
            Role = Role.User,
            TimeZoneIanaId = "UTC",
            Locked = false,
            LoginAttempt = 0,
            EmailConfirmed = true,
            AgreedServiceTerms = true,
            RegistrationToken = "",
            ResetPasswordToken = "",
            Message = "",
            Deleted = false
        };
        using var request = session.BuildJsonPostRequest("/Management/CreateAccount", payload);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("Password must be at least 8 characters", json);
    }

    /// <summary>Create account missing time zone returns specific error.</summary>
    [Fact]
    public async Task CreateAccount_MissingTimeZone_ReturnsSpecificError()
    {
        var session = await LoginAsAdminAsync();
        var payload = new
        {
            Email = "notz@test.com",
            Password = "ValidPassw0rd!",
            Nickname = "NoTz",
            AvatarImagePath = "",
            Role = Role.User,
            TimeZoneIanaId = "",
            Locked = false,
            LoginAttempt = 0,
            EmailConfirmed = true,
            AgreedServiceTerms = true,
            RegistrationToken = "",
            ResetPasswordToken = "",
            Message = "",
            Deleted = false
        };
        using var request = session.BuildJsonPostRequest("/Management/CreateAccount", payload);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("Time-zone ID cannot be empty", json); // TimeZoneId.Create (Core) message, not a controller-local string
    }

    // -- CreateAccount: success path ---------------------------------------------

    /// <summary>
    /// Verifies the real DB-write path end-to-end, including that <c>AvatarImagePath</c> —
    /// left unset by <c>Account.Create()</c> — gets filled in by the column's
    /// <c>HasDefaultValueSql</c> default rather than persisting as null.
    /// </summary>
    [Fact]
    public async Task CreateAccount_ValidPayload_PersistsAccountWithDefaultAvatarPath()
    {
        var session = await LoginAsAdminAsync();
        const string email = "created-account@test.com";
        using var request = session.BuildJsonPostRequest("/Management/CreateAccount", ValidAccountPayload(email));

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var account = db.Accounts.Single(a => a.Email == email);
        Assert.Equal("/upload/Management/Profile/default-avatar.jpg", account.AvatarImagePath);
        Assert.Equal(Role.User, account.Role);
        Assert.False(account.Deleted);
    }

    /// <summary>Create account duplicate email returns already created error.</summary>
    [Fact]
    public async Task CreateAccount_DuplicateEmail_ReturnsAlreadyCreatedError()
    {
        var session = await LoginAsAdminAsync();
        const string email = "duplicate-account@test.com";
        using var firstRequest = session.BuildJsonPostRequest("/Management/CreateAccount", ValidAccountPayload(email));
        await _client.SendAsync(firstRequest, TestContext.Current.CancellationToken);

        using var secondRequest = session.BuildJsonPostRequest("/Management/CreateAccount", new
        {
            Email = email,
            Password = "AnotherPassw0rd!",
            Nickname = "SecondNickname",
            AvatarImagePath = "",
            Role = Role.User,
            TimeZoneIanaId = "UTC",
            Locked = false,
            LoginAttempt = 0,
            EmailConfirmed = true,
            AgreedServiceTerms = true,
            RegistrationToken = "",
            ResetPasswordToken = "",
            Message = "",
            Deleted = false
        });
        var response = await _client.SendAsync(secondRequest, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("This account has already been created.", json);
    }

    /// <summary>
    /// The pre-check in <c>ManagementAccountService.CreateAccountAsync</c> only looks up by email,
    /// so a duplicate nickname on a *different* email is only ever caught by the real Postgres
    /// unique constraint (<c>Account_Nickname_unique</c>) raised from <c>CreateAsync</c> — this
    /// exercises that path end-to-end, including the resx lookup for the new error key/message.
    /// </summary>
    [Fact]
    public async Task CreateAccount_DuplicateNickname_ReturnsNicknameConflictError()
    {
        var session = await LoginAsAdminAsync();
        const string sharedNickname = "TakenAdminNickname";

        using var firstRequest = session.BuildJsonPostRequest("/Management/CreateAccount", new
        {
            Email = "nickname-owner@test.com",
            Password = "NewAccountPw1!",
            Nickname = sharedNickname,
            AvatarImagePath = "",
            Role = Role.User,
            TimeZoneIanaId = "UTC",
            Locked = false,
            LoginAttempt = 0,
            EmailConfirmed = true,
            AgreedServiceTerms = true,
            RegistrationToken = "",
            ResetPasswordToken = "",
            Message = "",
            Deleted = false
        });
        await _client.SendAsync(firstRequest, TestContext.Current.CancellationToken);

        using var secondRequest = session.BuildJsonPostRequest("/Management/CreateAccount", new
        {
            Email = "nickname-challenger@test.com",
            Password = "AnotherPassw0rd!",
            Nickname = sharedNickname,
            AvatarImagePath = "",
            Role = Role.User,
            TimeZoneIanaId = "UTC",
            Locked = false,
            LoginAttempt = 0,
            EmailConfirmed = true,
            AgreedServiceTerms = true,
            RegistrationToken = "",
            ResetPasswordToken = "",
            Message = "",
            Deleted = false
        });
        var response = await _client.SendAsync(secondRequest, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = System.Text.Json.JsonDocument.Parse(json);

        Assert.Equal(
            $"'{sharedNickname}' is a Nickname that already exists. Please enter another Nickname.",
            doc.RootElement.GetProperty("error").GetString());
    }

    // -- IsAccountExists ---------------------------------------------------------

    /// <summary>Is account exists seeded account returns success result.</summary>
    [Fact]
    public async Task IsAccountExists_SeededAccount_ReturnsSuccessResult()
    {
        var session = await LoginAsAdminAsync();
        const string email = "existing-account@test.com";
        SeedAccount(email);

        using var request = session.BuildJsonPostRequest($"/Management/IsAccountExists?email={email}");
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);
    }

    /// <summary>
    /// The edit form never reads the password hash or the registration / reset tokens, so the
    /// single-account lookup must not put them in the JSON response.
    /// </summary>
    [Fact]
    public async Task IsAccountExists_SeededAccount_DoesNotReturnSecretColumns()
    {
        var session = await LoginAsAdminAsync();
        const string email = "existing-secret-check@test.com";
        SeedAccount(email);

        using var request = session.BuildJsonPostRequest($"/Management/IsAccountExists?email={email}");
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);
        Assert.DoesNotContain("hashedPassword", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("registrationToken", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("resetPasswordToken", json, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Is account exists unknown email returns failure result.</summary>
    [Fact]
    public async Task IsAccountExists_UnknownEmail_ReturnsFailureResult()
    {
        var session = await LoginAsAdminAsync();
        using var request = session.BuildJsonPostRequest("/Management/IsAccountExists?email=nobody-xyz@test.com");

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("Fail to find the account by given email address", json);
    }

    // -- UpdateAccount ------------------------------------------------------------

    /// <summary>Update account existing account returns success result.</summary>
    [Fact]
    public async Task UpdateAccount_ExistingAccount_ReturnsSuccessResult()
    {
        var session = await LoginAsAdminAsync();
        const string email = "to-update@test.com";
        SeedAccount(email);

        using var updateRequest = session.BuildJsonPostRequest("/Management/UpdateAccount", new
        {
            Email = email,
            Password = "UnchangedPassw0rd!",
            Role = Role.User,
            TimeZoneIanaId = "Asia/Seoul",
            Locked = true,
            EmailConfirmed = true,
            AgreedServiceTerms = true,
            Message = "updated",
            Deleted = false
        });
        var response = await _client.SendAsync(updateRequest, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);
    }

    /// <summary>
    /// Regression test: an empty <c>Password</c> must mean "leave the current password
    /// unchanged", matching the admin edit form's real behavior (it submits whatever is
    /// currently in the password field, which is blank unless the admin typed a new one).
    /// Previously blocked by <c>[Required]</c> on <c>AccountInputViewModel.Password</c>, which
    /// made it impossible to edit any other field (Role, Locked, etc.) without also being forced
    /// to set a new password.
    /// </summary>
    [Fact]
    public async Task UpdateAccount_BlankPasswordAndMessage_StillSucceeds()
    {
        var session = await LoginAsAdminAsync();
        const string email = "blank-password-update@test.com";
        SeedAccount(email);

        using var updateRequest = session.BuildJsonPostRequest("/Management/UpdateAccount", new
        {
            Email = email,
            Password = "",
            Role = Role.Admin,
            TimeZoneIanaId = "Asia/Seoul",
            Locked = true,
            EmailConfirmed = true,
            AgreedServiceTerms = true,
            Message = "",
            Deleted = false
        });
        var response = await _client.SendAsync(updateRequest, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var account = db.Accounts.Single(a => a.Email == email);
        Assert.Equal(Role.Admin, account.Role);
        Assert.True(account.Locked);
        Assert.Equal("irrelevant-hash", account.HashedPassword); // unchanged from SeedAccount
    }

    /// <summary>
    /// The admin edit form can switch every account flag off and back on: confirmation, terms,
    /// deletion and lock, plus role, time zone and message. Each round trip is read back from the
    /// database.
    /// </summary>
    [Fact]
    public async Task UpdateAccount_TogglesEveryAdminFlag_BothWays()
    {
        var session = await LoginAsAdminAsync();
        const string email = "toggle-every-flag@test.com";
        SeedAccount(email);

        async Task UpdateAsync(bool on)
        {
            using var request = session.BuildJsonPostRequest("/Management/UpdateAccount", new
            {
                Email = email,
                Password = "",
                Role = on ? Role.Admin : Role.User,
                TimeZoneIanaId = on ? "Asia/Seoul" : "UTC",
                Locked = on,
                EmailConfirmed = !on,
                AgreedServiceTerms = !on,
                Message = on ? "sanctioned" : "",
                Deleted = on
            });
            var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Contains("\"result\":true", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }

        Maroik.Core.PostgreSQL.Models.Account Read()
        {
            using var scope = factory.Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Accounts.AsNoTracking().Single(a => a.Email == email);
        }

        await UpdateAsync(on: true);
        var flagged = Read();
        Assert.Equal((Role.Admin, "Asia/Seoul", true, false, false, "sanctioned", true),
            (flagged.Role, flagged.TimeZoneIanaId, flagged.Locked, flagged.EmailConfirmed, flagged.AgreedServiceTerms, flagged.Message, flagged.Deleted));

        await UpdateAsync(on: false);
        var cleared = Read();
        Assert.Equal((Role.User, "UTC", false, true, true, false),
            (cleared.Role, cleared.TimeZoneIanaId, cleared.Locked, cleared.EmailConfirmed, cleared.AgreedServiceTerms, cleared.Deleted));
        Assert.True(string.IsNullOrEmpty(cleared.Message));
    }

    /// <summary>
    /// Regression test: an out-of-range Role value must be rejected by Account.ChangeRole's own
    /// validation, not silently coerced to Role.User by the controller. Coercing it outside Core
    /// let an invalid Role bypass the Domain's validation entirely; the account's stored Role must
    /// stay unchanged when the request is rejected.
    /// </summary>
    [Fact]
    public async Task UpdateAccount_InvalidRole_IsRejected_AndDoesNotChangeTheStoredRole()
    {
        var session = await LoginAsAdminAsync();
        const string email = "invalid-role-update@test.com";
        SeedAccount(email);

        using var updateRequest = session.BuildJsonPostRequest("/Management/UpdateAccount", new
        {
            Email = email,
            Password = "",
            Role = "NotARealRole",
            TimeZoneIanaId = "Asia/Seoul",
            Locked = false,
            EmailConfirmed = true,
            AgreedServiceTerms = true,
            Message = "",
            Deleted = false
        });
        var response = await _client.SendAsync(updateRequest, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var account = db.Accounts.Single(a => a.Email == email);
        Assert.Equal(Role.User, account.Role); // unchanged from SeedAccount, not clamped
    }

    /// <summary>Update account unknown email returns failure result.</summary>
    [Fact]
    public async Task UpdateAccount_UnknownEmail_ReturnsFailureResult()
    {
        var session = await LoginAsAdminAsync();

        using var updateRequest = session.BuildJsonPostRequest("/Management/UpdateAccount", new
        {
            Email = "does-not-exist@test.com",
            Password = "UnchangedPassw0rd!",
            Role = Role.User,
            TimeZoneIanaId = "UTC",
            Locked = false,
            EmailConfirmed = true,
            AgreedServiceTerms = true,
            Message = "none",
            Deleted = false
        });
        var response = await _client.SendAsync(updateRequest, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("Email address is wrong", json);
    }

    // -- DeleteAccount ------------------------------------------------------------

    /// <summary>Delete account existing account soft deletes it.</summary>
    [Fact]
    public async Task DeleteAccount_ExistingAccount_SoftDeletesIt()
    {
        var session = await LoginAsAdminAsync();
        const string email = "to-delete@test.com";
        SeedAccount(email);

        using var deleteRequest = session.BuildJsonPostRequest("/Management/DeleteAccount", new { Email = email });
        var deleteResponse = await _client.SendAsync(deleteRequest, TestContext.Current.CancellationToken);
        string deleteJson = await deleteResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", deleteJson);

        // DeleteAccount is a soft delete (Deleted flag flipped), not a row removal — FindByEmailAsync
        // does not filter on Deleted, so verify the flag directly rather than via IsAccountExists.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var account = db.Accounts.Single(a => a.Email == email);
        Assert.True(account.Deleted);
    }

    /// <summary>Delete account unknown email returns failure result.</summary>
    [Fact]
    public async Task DeleteAccount_UnknownEmail_ReturnsFailureResult()
    {
        var session = await LoginAsAdminAsync();

        using var deleteRequest = session.BuildJsonPostRequest("/Management/DeleteAccount", new { Email = "nobody-to-delete@test.com" });
        var response = await _client.SendAsync(deleteRequest, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("Fail to find the account by given email address", json);
    }
}
