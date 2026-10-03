using Maroik.Core.Domain.Account;
using Maroik.Core.Domain.ValueObjects;
namespace Maroik.Core.Domain.Tests.Account;

/// <summary>
/// Unit tests for <see cref="Maroik.Core.Domain.Account.Account"/>.
/// Covers creation validation, email confirmation, password reset flow, login attempt tracking,
/// lock/unlock, admin update, soft-delete, profile update, and service-terms acceptance.
/// </summary>
public class AccountTests
{
    /// <summary>The fixed "current time" every domain call in this class receives.</summary>
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Creates a valid new account through <c>Account.Create</c>; each argument can be overridden per test.</summary>
    private static Domain.Account.Account ValidAccount(
        string email = "user@example.com",
        string hashedPassword = "hashed",
        string nickname = "TestUser",
        string role = "User",
        string timeZone = "UTC",
        string? registrationToken = "token-123",
        bool agreedServiceTerms = true)
        => Domain.Account.Account.Create(email, hashedPassword, nickname, AccountRole.Create(role).Value, timeZone, CurrencyCode.Create("KRW").Value, registrationToken, agreedServiceTerms, Now).Value;

    /// <summary>A token minted 25 hours before <see cref="Now"/>, past <see cref="GuidToken"/>'s 24h validity window.</summary>
    private static string ExpiredToken() => GuidToken.Generate(Now.AddHours(-25));

    // -- Create ---------------------------------------------------------------

    /// <summary>A new account's Created and Updated are the same instant: the one passed in.</summary>
    [Fact]
    public void Create_StampsCreatedAndUpdated_WithTheSameGivenInstant()
    {
        var account = ValidAccount();

        Assert.Equal(Now, account.Created);
        Assert.Equal(account.Created, account.Updated);
    }

    /// <summary>A registration token is checked against the time passed in: alive at exactly 24 h, refused one tick later.</summary>
    [Fact]
    public void ConfirmEmail_UsesTheGivenTime_ForTheTokenExpiry()
    {
        string token = GuidToken.Generate(Now);

        Assert.False(ValidAccount(registrationToken: token).ConfirmEmail(token, Now.AddHours(24)).IsError);
        Assert.Equal("Account.InvalidToken", ValidAccount(registrationToken: token).ConfirmEmail(token, Now.AddHours(24).AddTicks(1)).FirstError.Code);
    }

    /// <summary>Create returns account, when all inputs valid.</summary>
    [Fact]
    public void Create_ReturnsAccount_WhenAllInputsValid()
    {
        var result = Domain.Account.Account.Create("user@example.com", "hashed", "Nick", AccountRole.User, "UTC", CurrencyCode.Create("KRW").Value, "tok", true, Now);

        Assert.False(result.IsError);
        Assert.Equal("user@example.com", result.Value.Email.Value);
        Assert.Equal("Nick", result.Value.Nickname);
    }

    /// <summary>Create returns error, when email invalid.</summary>
    [Theory]
    [InlineData("notanemail")]
    [InlineData("")]
    [InlineData(null)]
    public void Create_ReturnsError_WhenEmailInvalid(string? email)
    {
        var result = Domain.Account.Account.Create(email!, "hashed", "Nick", AccountRole.User, "UTC", null, null, true, Now);

        Assert.True(result.IsError);
    }

    /// <summary>Create returns error, when nickname empty.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_ReturnsError_WhenNicknameEmpty(string? nickname)
    {
        var result = Domain.Account.Account.Create("user@example.com", "hashed", nickname!, AccountRole.User, "UTC", null, null, true, Now);

        Assert.True(result.IsError);
        Assert.Equal("Account.NicknameEmpty", result.FirstError.Code);
    }

    /// <summary>Create returns error, when nickname exceeds 255 characters.</summary>
    [Fact]
    public void Create_ReturnsError_WhenNicknameTooLong()
    {
        var result = Domain.Account.Account.Create("user@example.com", "hashed", new string('n', 256), AccountRole.User, "UTC", null, null, true, Now);

        Assert.True(result.IsError);
        Assert.Equal("Account.NicknameTooLong", result.FirstError.Code);
    }

    /// <summary>Create succeeds when nickname is exactly at the 255-character limit.</summary>
    [Fact]
    public void Create_ReturnsAccount_WhenNicknameAtMaxLength()
    {
        var result = Domain.Account.Account.Create("user@example.com", "hashed", new string('n', 255), AccountRole.User, "UTC", null, null, true, Now);

        Assert.False(result.IsError);
    }

    /// <summary>Create stores the normalized nickname, not the raw input.</summary>
    [Fact]
    public void Create_StoresNormalizedNickname()
    {
        var result = Domain.Account.Account.Create("user@example.com", "hashed", "  Ｂｏｂ   Smith ", AccountRole.User, "UTC", null, null, true, Now);

        Assert.False(result.IsError);
        Assert.Equal("Bob Smith", result.Value.Nickname);
    }

    /// <summary>Create rejects a reserved nickname.</summary>
    [Theory]
    [InlineData("Admin")]
    [InlineData("login")]
    [InlineData("A d m i n")]
    public void Create_ReturnsError_WhenNicknameIsReserved(string nickname)
    {
        var result = Domain.Account.Account.Create("user@example.com", "hashed", nickname, AccountRole.User, "UTC", null, null, true, Now);

        Assert.True(result.IsError);
        Assert.Equal("Account.NicknameReserved", result.FirstError.Code);
    }

    /// <summary>Create accepts a reserved nickname when the caller (an administrator) allows it.</summary>
    [Fact]
    public void Create_AcceptsReservedNickname_WhenAllowReservedNickname()
    {
        var result = Domain.Account.Account.Create("user@example.com", "hashed", "Admin", AccountRole.Admin, "UTC", null, null, true, Now, allowReservedNickname: true);

        Assert.False(result.IsError);
        Assert.Equal("Admin", result.Value.Nickname);
    }

    /// <summary>Create rejects a nickname containing invisible characters.</summary>
    [Fact]
    public void Create_ReturnsError_WhenNicknameHasInvisibleCharacters()
    {
        var result = Domain.Account.Account.Create("user@example.com", "hashed", "Bo\u200Bb", AccountRole.User, "UTC", null, null, true, Now);

        Assert.True(result.IsError);
        Assert.Equal("Account.NicknameInvalidCharacters", result.FirstError.Code);
    }

    /// <summary>Create returns error, when password empty.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_ReturnsError_WhenPasswordEmpty(string? password)
    {
        var result = Domain.Account.Account.Create("user@example.com", password!, "Nick", AccountRole.User, "UTC", null, null, true, Now);

        Assert.True(result.IsError);
        Assert.Equal("Account.PasswordEmpty", result.FirstError.Code);
    }

    /// <summary>Create returns error, when time zone invalid.</summary>
    [Fact]
    public void Create_ReturnsError_WhenTimeZoneInvalid()
    {
        var result = Domain.Account.Account.Create("user@example.com", "hashed", "Nick", AccountRole.User, "Not/Valid", null, null, true, Now);

        Assert.True(result.IsError);
        Assert.Equal("TimeZoneId.Invalid", result.FirstError.Code);
    }


    // -- ConfirmEmail ---------------------------------------------------------

    /// <summary>Confirm email succeeds, when token matches.</summary>
    [Fact]
    public void ConfirmEmail_Succeeds_WhenTokenMatches()
    {
        string token = GuidToken.Generate(Now);
        var account = ValidAccount(registrationToken: token);

        var result = account.ConfirmEmail(token, Now);

        Assert.False(result.IsError);
        Assert.True(account.EmailConfirmed);
        Assert.Null(account.RegistrationToken);
    }

    /// <summary>Confirm email returns error, when already confirmed.</summary>
    [Fact]
    public void ConfirmEmail_ReturnsError_WhenAlreadyConfirmed()
    {
        string token = GuidToken.Generate(Now);
        var account = ValidAccount(registrationToken: token);
        account.ConfirmEmail(token, Now);

        var result = account.ConfirmEmail(token, Now);

        Assert.True(result.IsError);
        Assert.Equal("Account.AlreadyConfirmed", result.FirstError.Code);
    }

    /// <summary>Confirm email returns error, when the stored token has passed its 24h TTL, even
    /// though it still matches the candidate — expiry is enforced by the aggregate itself, not just
    /// by a caller pre-check.</summary>
    [Fact]
    public void ConfirmEmail_ReturnsError_WhenTokenExpired()
    {
        string token = ExpiredToken();
        var account = ValidAccount(registrationToken: token);

        var result = account.ConfirmEmail(token, Now);

        Assert.True(result.IsError);
        Assert.Equal("Account.InvalidToken", result.FirstError.Code);
        Assert.False(account.EmailConfirmed);
    }

    /// <summary>Confirm email returns error, when token mismatch.</summary>
    [Fact]
    public void ConfirmEmail_ReturnsError_WhenTokenMismatch()
    {
        var account = ValidAccount(registrationToken: "correct");

        var result = account.ConfirmEmail("wrong", Now);

        Assert.True(result.IsError);
        Assert.Equal("Account.InvalidToken", result.FirstError.Code);
        Assert.False(account.EmailConfirmed);
    }

    /// <summary>
    /// Regression test: a null/empty stored RegistrationToken must never match, even a null/empty
    /// candidate token, unlike a plain <c>==</c> comparison (where null == null is true).
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ConfirmEmail_ReturnsError_WhenStoredTokenIsNullOrEmpty_EvenForSameCandidate(string? storedToken)
    {
        var account = ValidAccount(registrationToken: storedToken);

        var result = account.ConfirmEmail(storedToken ?? "", Now);

        Assert.True(result.IsError);
        Assert.Equal("Account.InvalidToken", result.FirstError.Code);
        Assert.False(account.EmailConfirmed);
    }

    // -- RequestPasswordReset -------------------------------------------------

    /// <summary>Request password reset succeeds, when email confirmed.</summary>
    [Fact]
    public void RequestPasswordReset_Succeeds_WhenEmailConfirmed()
    {
        string token = GuidToken.Generate(Now);
        var account = ValidAccount(registrationToken: token);
        account.ConfirmEmail(token, Now);

        var result = account.RequestPasswordReset("reset-token", Now);

        Assert.False(result.IsError);
        Assert.Equal("reset-token", account.ResetPasswordToken);
    }

    /// <summary>Request password reset returns error, when email not confirmed.</summary>
    [Fact]
    public void RequestPasswordReset_ReturnsError_WhenEmailNotConfirmed()
    {
        var account = ValidAccount();

        var result = account.RequestPasswordReset("reset-token", Now);

        Assert.True(result.IsError);
        Assert.Equal("Account.NotConfirmed", result.FirstError.Code);
    }

    /// <summary>Request password reset succeeds, when locked, since it is the way a locked account is recovered.</summary>
    [Fact]
    public void RequestPasswordReset_Succeeds_WhenLocked()
    {
        string token = GuidToken.Generate(Now);
        var account = ValidAccount(registrationToken: token);
        account.ConfirmEmail(token, Now);
        account.Lock(Now, "reason");

        var result = account.RequestPasswordReset("reset-token", Now);

        Assert.False(result.IsError);
        Assert.Equal("reset-token", account.ResetPasswordToken);
    }

    // -- ResetPassword --------------------------------------------------------

    /// <summary>Reset password succeeds, when token valid.</summary>
    [Fact]
    public void ResetPassword_Succeeds_WhenTokenValid()
    {
        string registrationToken = GuidToken.Generate(Now);
        var account = ValidAccount(registrationToken: registrationToken);
        account.ConfirmEmail(registrationToken, Now);
        string resetToken = GuidToken.Generate(Now);
        account.RequestPasswordReset(resetToken, Now);

        var result = account.ResetPassword(resetToken, "newHash", Now);

        Assert.False(result.IsError);
        Assert.Equal("newHash", account.HashedPassword);
        Assert.Null(account.ResetPasswordToken);
    }

    /// <summary>Reset password returns error, when token invalid.</summary>
    [Fact]
    public void ResetPassword_ReturnsError_WhenTokenInvalid()
    {
        string registrationToken = GuidToken.Generate(Now);
        var account = ValidAccount(registrationToken: registrationToken);
        account.ConfirmEmail(registrationToken, Now);
        account.RequestPasswordReset(GuidToken.Generate(Now), Now);

        var result = account.ResetPassword("wrong-tok", "newHash", Now);

        Assert.True(result.IsError);
        Assert.Equal("Account.InvalidToken", result.FirstError.Code);
    }

    /// <summary>Reset password returns error, when the stored token has passed its 24h TTL, even
    /// though it still matches the candidate.</summary>
    [Fact]
    public void ResetPassword_ReturnsError_WhenTokenExpired()
    {
        string registrationToken = GuidToken.Generate(Now);
        var account = ValidAccount(registrationToken: registrationToken);
        account.ConfirmEmail(registrationToken, Now);
        string expiredResetToken = ExpiredToken();
        account.RequestPasswordReset(expiredResetToken, Now);

        var result = account.ResetPassword(expiredResetToken, "newHash", Now);

        Assert.True(result.IsError);
        Assert.Equal("Account.InvalidToken", result.FirstError.Code);
    }

    /// <summary>Reset password returns error, when new password empty.</summary>
    [Fact]
    public void ResetPassword_ReturnsError_WhenNewPasswordEmpty()
    {
        string registrationToken = GuidToken.Generate(Now);
        var account = ValidAccount(registrationToken: registrationToken);
        account.ConfirmEmail(registrationToken, Now);
        string resetToken = GuidToken.Generate(Now);
        account.RequestPasswordReset(resetToken, Now);

        var result = account.ResetPassword(resetToken, "", Now);

        Assert.True(result.IsError);
        Assert.Equal("Account.PasswordEmpty", result.FirstError.Code);
    }

    // -- Login attempt tracking ------------------------------------------------

    /// <summary>Record login failure increments counter.</summary>
    [Fact]
    public void RecordLoginFailure_IncrementsCounter()
    {
        var account = ValidAccount();

        account.RecordLoginFailure(maxAttempts: 5, Now);
        account.RecordLoginFailure(maxAttempts: 5, Now);

        Assert.Equal(2, account.LoginAttempt);
        Assert.False(account.Locked);
    }

    /// <summary>Record login failure locks account, when max attempts reached.</summary>
    [Fact]
    public void RecordLoginFailure_LocksAccount_WhenMaxAttemptsReached()
    {
        var account = ValidAccount();

        account.RecordLoginFailure(maxAttempts: 3, Now);
        account.RecordLoginFailure(maxAttempts: 3, Now);
        account.RecordLoginFailure(maxAttempts: 3, Now);

        Assert.Equal(3, account.LoginAttempt);
        Assert.True(account.Locked);
    }

    /// <summary>Record login failure locks account, when counter exceeds max.</summary>
    [Fact]
    public void RecordLoginFailure_LocksAccount_WhenCounterExceedsMax()
    {
        var account = ValidAccount();

        account.RecordLoginFailure(maxAttempts: 2, Now);
        account.RecordLoginFailure(maxAttempts: 2, Now);
        account.RecordLoginFailure(maxAttempts: 2, Now);

        Assert.True(account.Locked);
    }

    /// <summary>Reset login attempt sets counter to zero.</summary>
    [Fact]
    public void ResetLoginAttempt_SetsCounterToZero()
    {
        var account = ValidAccount();
        account.RecordLoginFailure(maxAttempts: 5, Now);
        account.RecordLoginFailure(maxAttempts: 5, Now);

        account.ResetLoginAttempt(Now);

        Assert.Equal(0, account.LoginAttempt);
    }

    // -- Lock / Unlock --------------------------------------------------------

    /// <summary>Lock sets locked true and stores message.</summary>
    [Fact]
    public void Lock_SetsLockedTrueAndStoresMessage()
    {
        var account = ValidAccount();

        account.Lock(Now, "spam");

        Assert.True(account.Locked);
        Assert.Equal("spam", account.Message);
    }

    /// <summary>A lock from failed logins keeps the security stamp: it refuses new logins but ends no session.</summary>
    [Fact]
    public void RecordLoginFailure_LockingTheAccount_KeepsTheSecurityStamp()
    {
        var account = ValidAccount();
        string stamp = account.SecurityStamp;

        for (int i = 0; i < 3; i++)
            account.RecordLoginFailure(maxAttempts: 3, Now);

        Assert.True(account.Locked);
        Assert.Equal(stamp, account.SecurityStamp);
    }

    /// <summary>An administrator's lock also replaces the security stamp, which ends every open session.</summary>
    [Fact]
    public void LockAndEndSessions_LocksTheAccount_AndReplacesTheSecurityStamp()
    {
        var account = ValidAccount();
        string stamp = account.SecurityStamp;

        account.LockAndEndSessions(Now);

        Assert.True(account.Locked);
        Assert.NotEqual(stamp, account.SecurityStamp);
        Assert.False(string.IsNullOrEmpty(account.SecurityStamp));
    }

    /// <summary>Unlock sets locked false and resets attempts.</summary>
    [Fact]
    public void Unlock_SetsLockedFalseAndResetsAttempts()
    {
        var account = ValidAccount();
        account.RecordLoginFailure(maxAttempts: 5, Now);
        account.Lock(Now);

        account.Unlock(Now);

        Assert.False(account.Locked);
        Assert.Equal(0, account.LoginAttempt);
    }

    // -- Admin operations -----------------------------------------------------

    /// <summary>ChangeRole sets a valid role.</summary>
    [Theory]
    [InlineData(Role.Admin)]
    [InlineData(Role.User)]
    public void ChangeRole_SetsTheRole_WhenValid(string role)
    {
        var account = ValidAccount();

        account.ChangeRole(AccountRole.Create(role).Value, Now);

        Assert.Equal(role, account.Role.Value);
    }

    /// <summary>ChangeTimeZone sets a valid IANA zone.</summary>
    [Fact]
    public void ChangeTimeZone_SetsTheZone_WhenValid()
    {
        var account = ValidAccount();

        var result = account.ChangeTimeZone("Asia/Seoul", Now);

        Assert.False(result.IsError);
        Assert.Equal("Asia/Seoul", account.TimeZone.Value);
    }

    /// <summary>ChangeTimeZone rejects an unknown zone and keeps the current one.</summary>
    [Fact]
    public void ChangeTimeZone_ReturnsError_WhenTimeZoneInvalid()
    {
        var account = ValidAccount();
        string before = account.TimeZone.Value;

        var result = account.ChangeTimeZone("Not/Valid", Now);

        Assert.True(result.IsError);
        Assert.Equal("TimeZoneId.Invalid", result.FirstError.Code);
        Assert.Equal(before, account.TimeZone.Value);
    }

    /// <summary>RevokeEmailConfirmation marks a confirmed account unconfirmed again.</summary>
    [Fact]
    public void RevokeEmailConfirmation_ClearsTheConfirmedFlag()
    {
        var account = ValidAccount();
        account.ForceConfirmEmail(Now);

        account.RevokeEmailConfirmation(Now);

        Assert.False(account.EmailConfirmed);
    }

    /// <summary>RevokeServiceTerms clears the accepted-terms flag.</summary>
    [Fact]
    public void RevokeServiceTerms_ClearsTheAcceptedFlag()
    {
        var account = ValidAccount();
        account.AcceptServiceTerms(Now);

        account.RevokeServiceTerms(Now);

        Assert.False(account.AgreedServiceTerms);
    }

    /// <summary>Restore undoes a soft delete.</summary>
    [Fact]
    public void Restore_ClearsTheDeletedFlag()
    {
        var account = ValidAccount();
        account.SoftDelete(Now);

        account.Restore(Now);

        Assert.False(account.Deleted);
    }

    /// <summary>Lock keeps the failed-login counter; only Unlock resets it.</summary>
    [Fact]
    public void Lock_KeepsTheLoginAttemptCount()
    {
        var account = ValidAccount();
        account.RecordLoginFailure(maxAttempts: 5, Now);
        account.RecordLoginFailure(maxAttempts: 5, Now);

        account.Lock(Now);

        Assert.Equal(2, account.LoginAttempt);
    }

    // -- SoftDelete -----------------------------------------------------------

    /// <summary>Soft delete sets deleted true.</summary>
    [Fact]
    public void SoftDelete_SetsDeletedTrue()
    {
        var account = ValidAccount();

        account.SoftDelete(Now);

        Assert.True(account.Deleted);
    }

    // -- ReplaceUnconfirmedRegistration ---------------------------------------

    /// <summary>
    /// A re-registration of an unconfirmed account replaces the password, nickname, time zone,
    /// terms flag and token with the latest registrant's values — nobody has proven they own the
    /// address yet, so the first registrant has no claim to keep.
    /// </summary>
    [Fact]
    public void ReplaceUnconfirmedRegistration_ReplacesTheCredentials_WhenUnconfirmed()
    {
        var account = ValidAccount(hashedPassword: "old-hash", nickname: "Squatter", timeZone: "UTC", agreedServiceTerms: false);
        string oldStamp = account.SecurityStamp;
        string newToken = GuidToken.Generate(Now);

        var result = account.ReplaceUnconfirmedRegistration("new-hash", "  Real   Owner ", "Asia/Seoul", newToken, agreedServiceTerms: true, Now);

        Assert.False(result.IsError);
        Assert.Equal("new-hash", account.HashedPassword);
        Assert.Equal("Real Owner", account.Nickname);
        Assert.Equal("Asia/Seoul", account.TimeZone.Value);
        Assert.Equal(newToken, account.RegistrationToken);
        Assert.True(account.AgreedServiceTerms);
        Assert.NotEqual(oldStamp, account.SecurityStamp);
        Assert.False(account.EmailConfirmed);
    }

    /// <summary>The link mailed for the replaced registration can no longer confirm the account; the new one can.</summary>
    [Fact]
    public void ReplaceUnconfirmedRegistration_InvalidatesThePreviouslyMailedToken()
    {
        string oldToken = GuidToken.Generate(Now);
        string newToken = GuidToken.Generate(Now);
        var account = ValidAccount(registrationToken: oldToken);

        account.ReplaceUnconfirmedRegistration("new-hash", "Owner", "UTC", newToken, agreedServiceTerms: true, Now);

        Assert.True(account.ConfirmEmail(oldToken, Now).IsError);
        Assert.False(account.ConfirmEmail(newToken, Now).IsError);
    }

    /// <summary>A confirmed account's credentials can never be replaced by a registration.</summary>
    [Fact]
    public void ReplaceUnconfirmedRegistration_ReturnsConflict_AndChangesNothing_WhenAlreadyConfirmed()
    {
        string token = GuidToken.Generate(Now);
        var account = ValidAccount(hashedPassword: "owner-hash", nickname: "Owner", registrationToken: token);
        account.ConfirmEmail(token, Now);

        var result = account.ReplaceUnconfirmedRegistration("attacker-hash", "Attacker", "UTC", GuidToken.Generate(Now), agreedServiceTerms: true, Now);

        Assert.True(result.IsError);
        Assert.Equal("Account.AlreadyConfirmed", result.FirstError.Code);
        Assert.Equal("owner-hash", account.HashedPassword);
        Assert.Equal("Owner", account.Nickname);
        Assert.Null(account.RegistrationToken);
    }

    /// <summary>An invalid nickname, time zone or empty hash is refused and leaves the account untouched.</summary>
    [Theory]
    [InlineData("new-hash", "", "UTC", "Account.NicknameEmpty")]
    [InlineData("new-hash", "admin", "UTC", "Account.NicknameReserved")]
    [InlineData("new-hash", "Owner", "Not/AZone", "TimeZoneId.Invalid")]
    [InlineData("", "Owner", "UTC", "Account.PasswordEmpty")]
    public void ReplaceUnconfirmedRegistration_ReturnsError_AndChangesNothing_WhenInputIsInvalid(
        string hashedPassword, string nickname, string timeZone, string expectedCode)
    {
        string token = GuidToken.Generate(Now);
        var account = ValidAccount(hashedPassword: "old-hash", nickname: "Squatter", registrationToken: token);

        var result = account.ReplaceUnconfirmedRegistration(hashedPassword, nickname, timeZone, GuidToken.Generate(Now), agreedServiceTerms: true, Now);

        Assert.True(result.IsError);
        Assert.Equal(expectedCode, result.FirstError.Code);
        Assert.Equal("old-hash", account.HashedPassword);
        Assert.Equal("Squatter", account.Nickname);
        Assert.Equal(token, account.RegistrationToken);
    }

    // -- UpdateProfile --------------------------------------------------------

    /// <summary>Update profile updates all fields.</summary>
    [Fact]
    public void UpdateProfile_UpdatesAllFields()
    {
        var account = ValidAccount();
        var newTz = TimeZoneId.Create("Asia/Seoul").Value;

        account.UpdateProfile("/avatar.png", newTz, CurrencyCode.Create("USD").Value, Now);

        Assert.Equal("/avatar.png", account.AvatarImagePath);
        Assert.Equal("Asia/Seoul", account.TimeZone.Value);
        Assert.Equal("USD", account.DefaultMonetaryUnit?.Value);
    }

    /// <summary>Update profile clears avatar and currency, when null.</summary>
    [Fact]
    public void UpdateProfile_ClearsAvatarAndCurrency_WhenNull()
    {
        var account = ValidAccount();
        var tz = TimeZoneId.Create("UTC").Value;
        account.UpdateProfile("/avatar.png", tz, CurrencyCode.Create("KRW").Value, Now);

        account.UpdateProfile(null, tz, null, Now);

        Assert.Null(account.AvatarImagePath);
        Assert.Null(account.DefaultMonetaryUnit);
    }

    // -- ChangePassword -------------------------------------------------------

    /// <summary>Change password replaces hashed password.</summary>
    [Fact]
    public void ChangePassword_ReplacesHashedPassword()
    {
        var account = ValidAccount();

        account.ChangePassword("newHash", Now);

        Assert.Equal("newHash", account.HashedPassword);
    }

    /// <summary>
    /// A signed-in owner whose account was locked by someone else's failed logins proves the current password to change
    /// it; the change unlocks the account (counter and lock message cleared), or the sign-in it asks for would be refused.
    /// </summary>
    [Fact]
    public void ChangePassword_UnlocksAnAccountLockedByFailedLogins()
    {
        var account = ValidAccount();
        for (int i = 0; i < 3; i++)
            account.RecordLoginFailure(maxAttempts: 3, Now);

        account.ChangePassword("newHash", Now);

        Assert.False(account.Locked);
        Assert.Equal(0, account.LoginAttempt);
        Assert.Null(account.Message);
    }

    /// <summary>On an unlocked account the change clears the failed-login counter but keeps the account's message.</summary>
    [Fact]
    public void ChangePassword_OnAnUnlockedAccount_ResetsTheCounter_AndKeepsTheMessage()
    {
        var account = ValidAccount();
        account.RecordLoginFailure(maxAttempts: 3, Now);
        account.SetMessage("note", Now);

        account.ChangePassword("newHash", Now);

        Assert.False(account.Locked);
        Assert.Equal(0, account.LoginAttempt);
        Assert.Equal("note", account.Message);
    }

    // -- SetMessage -----------------------------------------------------------

    /// <summary>Set message stores text.</summary>
    [Fact]
    public void SetMessage_StoresText()
    {
        var account = ValidAccount();

        account.SetMessage("Under review", Now);

        Assert.Equal("Under review", account.Message);
    }

    /// <summary>Set message clears message, when null.</summary>
    [Fact]
    public void SetMessage_ClearsMessage_WhenNull()
    {
        var account = ValidAccount();
        account.SetMessage("some message", Now);

        account.SetMessage(null, Now);

        Assert.Null(account.Message);
    }

    // -- ForceConfirmEmail ----------------------------------------------------

    /// <summary>Force confirm email confirms email and clears token.</summary>
    [Fact]
    public void ForceConfirmEmail_ConfirmsEmailAndClearsToken()
    {
        var account = ValidAccount(registrationToken: "tok");

        account.ForceConfirmEmail(Now);

        Assert.True(account.EmailConfirmed);
        Assert.Null(account.RegistrationToken);
    }

    /// <summary>Force confirm email is idempotent, when already confirmed.</summary>
    [Fact]
    public void ForceConfirmEmail_IsIdempotent_WhenAlreadyConfirmed()
    {
        var account = ValidAccount(registrationToken: "tok");
        account.ForceConfirmEmail(Now);

        account.ForceConfirmEmail(Now);

        Assert.True(account.EmailConfirmed);
    }

    // -- AcceptServiceTerms ---------------------------------------------------

    /// <summary>Accept service terms sets agreed to true.</summary>
    [Fact]
    public void AcceptServiceTerms_SetsAgreedToTrue()
    {
        var account = ValidAccount(agreedServiceTerms: false);

        account.AcceptServiceTerms(Now);

        Assert.True(account.AgreedServiceTerms);
    }

    // -- Empty password hashes -------------------------------------------------

    /// <summary>A self-service password change refuses an empty or blank hash and changes nothing.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ChangePassword_RejectsAnEmptyHash_AndKeepsTheCurrentCredentials(string hash)
    {
        var account = ValidAccount(hashedPassword: "old-hash");
        string stamp = account.SecurityStamp;

        var result = account.ChangePassword(hash, Now);

        Assert.True(result.IsError);
        Assert.Equal("Account.PasswordEmpty", result.FirstError.Code);
        Assert.Equal("old-hash", account.HashedPassword);
        Assert.Equal(stamp, account.SecurityStamp);
    }

    /// <summary>An admin password reset refuses an empty or blank hash and changes nothing.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AdminResetPassword_RejectsAnEmptyHash_AndKeepsTheCurrentCredentials(string hash)
    {
        var account = ValidAccount(hashedPassword: "old-hash");
        string stamp = account.SecurityStamp;

        var result = account.AdminResetPassword(hash, Now);

        Assert.True(result.IsError);
        Assert.Equal("Account.PasswordEmpty", result.FirstError.Code);
        Assert.Equal("old-hash", account.HashedPassword);
        Assert.Equal(stamp, account.SecurityStamp);
        Assert.False(account.MustChangePassword);
    }

    /// <summary>Both password replacements report success for a real hash.</summary>
    [Fact]
    public void ChangePassword_And_AdminResetPassword_Succeed_ForANonEmptyHash()
    {
        var account = ValidAccount();

        Assert.False(account.AdminResetPassword("temp-hash", Now).IsError);
        Assert.False(account.ChangePassword("new-hash", Now).IsError);
        Assert.Equal("new-hash", account.HashedPassword);
    }

    // -- SecurityStamp / MustChangePassword ------------------------------------

    /// <summary>A newly created account gets a non-empty security stamp and is not forced to change its password.</summary>
    [Fact]
    public void Create_SetsNonEmptySecurityStamp_AndMustChangePasswordFalse()
    {
        var account = ValidAccount();

        Assert.False(string.IsNullOrEmpty(account.SecurityStamp));
        Assert.False(account.MustChangePassword);
    }

    /// <summary>Self-service change password regenerates the security stamp.</summary>
    [Fact]
    public void ChangePassword_RegeneratesSecurityStamp()
    {
        var account = ValidAccount();
        string originalStamp = account.SecurityStamp;

        account.ChangePassword("newHash", Now);

        Assert.NotEqual(originalStamp, account.SecurityStamp);
    }

    /// <summary>Self-service change password clears a pending forced password change.</summary>
    [Fact]
    public void ChangePassword_ClearsMustChangePassword()
    {
        var account = ValidAccount();
        account.AdminResetPassword("tempHash", Now);

        account.ChangePassword("newHash", Now);

        Assert.False(account.MustChangePassword);
    }

    /// <summary>Admin reset password replaces the hash, forces a password change, and regenerates the security stamp.</summary>
    [Fact]
    public void AdminResetPassword_SetsHashForcesChange_AndRegeneratesSecurityStamp()
    {
        var account = ValidAccount();
        string originalStamp = account.SecurityStamp;

        account.AdminResetPassword("tempHash", Now);

        Assert.Equal("tempHash", account.HashedPassword);
        Assert.True(account.MustChangePassword);
        Assert.NotEqual(originalStamp, account.SecurityStamp);
    }

    /// <summary>Self-service change password discards a pending reset token, so an earlier reset link cannot overwrite the new password.</summary>
    [Fact]
    public void ChangePassword_ClearsPendingResetPasswordToken()
    {
        string registrationToken = GuidToken.Generate(Now);
        var account = ValidAccount(registrationToken: registrationToken);
        account.ConfirmEmail(registrationToken, Now);
        string resetToken = GuidToken.Generate(Now);
        account.RequestPasswordReset(resetToken, Now);

        account.ChangePassword("newHash", Now);

        Assert.Null(account.ResetPasswordToken);
        Assert.True(account.ResetPassword(resetToken, "attackerHash", Now).IsError);
        Assert.Equal("newHash", account.HashedPassword);
    }

    /// <summary>Admin reset discards a pending reset token, so an earlier reset link cannot bypass the forced password change.</summary>
    [Fact]
    public void AdminResetPassword_ClearsPendingResetPasswordToken()
    {
        string registrationToken = GuidToken.Generate(Now);
        var account = ValidAccount(registrationToken: registrationToken);
        account.ConfirmEmail(registrationToken, Now);
        string resetToken = GuidToken.Generate(Now);
        account.RequestPasswordReset(resetToken, Now);

        account.AdminResetPassword("tempHash", Now);

        Assert.Null(account.ResetPasswordToken);
        Assert.True(account.ResetPassword(resetToken, "attackerHash", Now).IsError);
        Assert.Equal("tempHash", account.HashedPassword);
        Assert.True(account.MustChangePassword);
    }

    /// <summary>Token-based password reset regenerates the security stamp and clears a pending forced password change.</summary>
    [Fact]
    public void ResetPassword_RegeneratesSecurityStamp_AndClearsMustChangePassword()
    {
        string registrationToken = GuidToken.Generate(Now);
        var account = ValidAccount(registrationToken: registrationToken);
        account.ConfirmEmail(registrationToken, Now);
        // The admin reset comes first: it discards any reset link requested before it, so the
        // link used below must be requested afterward.
        account.AdminResetPassword("tempHash", Now);
        string resetToken = GuidToken.Generate(Now);
        account.RequestPasswordReset(resetToken, Now);
        string stampBeforeReset = account.SecurityStamp;

        var result = account.ResetPassword(resetToken, "newHash", Now);

        Assert.False(result.IsError);
        Assert.NotEqual(stampBeforeReset, account.SecurityStamp);
        Assert.False(account.MustChangePassword);
    }

    // -- RegenerateRegistrationToken -------------------------------------------

    /// <summary>Regenerate registration token replaces the stored token for an unconfirmed account (even one that is still valid).</summary>
    [Fact]
    public void RegenerateRegistrationToken_ReplacesTheToken_WhenNotYetConfirmed()
    {
        var account = ValidAccount(registrationToken: "old-token");

        account.RegenerateRegistrationToken("fresh-token", Now);

        Assert.Equal("fresh-token", account.RegistrationToken);
    }

    /// <summary>Once the e-mail is confirmed there is nothing left to confirm, so regenerating changes nothing.</summary>
    [Fact]
    public void RegenerateRegistrationToken_IsANoOp_OnceConfirmed()
    {
        string token = GuidToken.Generate(Now);
        var account = ValidAccount(registrationToken: token);
        account.ConfirmEmail(token, Now);

        account.RegenerateRegistrationToken("fresh-token", Now);

        Assert.Null(account.RegistrationToken);
    }
}
