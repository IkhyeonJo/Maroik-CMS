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
    /// <summary>Creates a valid new account through <c>Account.Create</c>; each argument can be overridden per test.</summary>
    private static Domain.Account.Account ValidAccount(
        string email = "user@example.com",
        string hashedPassword = "hashed",
        string nickname = "TestUser",
        string role = "User",
        string timeZone = "UTC",
        string? registrationToken = "token-123",
        bool agreedServiceTerms = true)
        => Domain.Account.Account.Create(email, hashedPassword, nickname, role, timeZone, "KRW", registrationToken, agreedServiceTerms).Value;

    /// <summary>
    /// Builds a token using the same encoding as <see cref="GuidToken.Generate"/> but timestamped
    /// 25 hours in the past, i.e. past <see cref="GuidToken"/>'s 24h validity window.
    /// </summary>
    private static string ExpiredToken()
    {
        byte[] time = BitConverter.GetBytes(DateTime.UtcNow.AddHours(-25).ToBinary());
        byte[] key = Guid.NewGuid().ToByteArray();
        return Convert.ToBase64String([.. time, .. key]);
    }

    // -- Create ---------------------------------------------------------------

    /// <summary>Create returns account, when all inputs valid.</summary>
    [Fact]
    public void Create_ReturnsAccount_WhenAllInputsValid()
    {
        var result = Domain.Account.Account.Create("user@example.com", "hashed", "Nick", "User", "UTC", "KRW", "tok", true);

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
        var result = Domain.Account.Account.Create(email!, "hashed", "Nick", "User", "UTC", null, null, true);

        Assert.True(result.IsError);
    }

    /// <summary>Create returns error, when nickname empty.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_ReturnsError_WhenNicknameEmpty(string? nickname)
    {
        var result = Domain.Account.Account.Create("user@example.com", "hashed", nickname!, "User", "UTC", null, null, true);

        Assert.True(result.IsError);
        Assert.Equal("Account.NicknameEmpty", result.FirstError.Code);
    }

    /// <summary>Create returns error, when nickname exceeds 255 characters.</summary>
    [Fact]
    public void Create_ReturnsError_WhenNicknameTooLong()
    {
        var result = Domain.Account.Account.Create("user@example.com", "hashed", new string('n', 256), "User", "UTC", null, null, true);

        Assert.True(result.IsError);
        Assert.Equal("Account.NicknameTooLong", result.FirstError.Code);
    }

    /// <summary>Create succeeds when nickname is exactly at the 255-character limit.</summary>
    [Fact]
    public void Create_ReturnsAccount_WhenNicknameAtMaxLength()
    {
        var result = Domain.Account.Account.Create("user@example.com", "hashed", new string('n', 255), "User", "UTC", null, null, true);

        Assert.False(result.IsError);
    }

    /// <summary>Create stores the normalized nickname, not the raw input.</summary>
    [Fact]
    public void Create_StoresNormalizedNickname()
    {
        var result = Domain.Account.Account.Create("user@example.com", "hashed", "  Ｂｏｂ   Smith ", "User", "UTC", null, null, true);

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
        var result = Domain.Account.Account.Create("user@example.com", "hashed", nickname, "User", "UTC", null, null, true);

        Assert.True(result.IsError);
        Assert.Equal("Account.NicknameReserved", result.FirstError.Code);
    }

    /// <summary>Create accepts a reserved nickname when the caller (an administrator) allows it.</summary>
    [Fact]
    public void Create_AcceptsReservedNickname_WhenAllowReservedNickname()
    {
        var result = Domain.Account.Account.Create("user@example.com", "hashed", "Admin", "Admin", "UTC", null, null, true, allowReservedNickname: true);

        Assert.False(result.IsError);
        Assert.Equal("Admin", result.Value.Nickname);
    }

    /// <summary>Create rejects a nickname containing invisible characters.</summary>
    [Fact]
    public void Create_ReturnsError_WhenNicknameHasInvisibleCharacters()
    {
        var result = Domain.Account.Account.Create("user@example.com", "hashed", "Bo\u200Bb", "User", "UTC", null, null, true);

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
        var result = Domain.Account.Account.Create("user@example.com", password!, "Nick", "User", "UTC", null, null, true);

        Assert.True(result.IsError);
        Assert.Equal("Account.PasswordEmpty", result.FirstError.Code);
    }

    /// <summary>Create returns error, when time zone invalid.</summary>
    [Fact]
    public void Create_ReturnsError_WhenTimeZoneInvalid()
    {
        var result = Domain.Account.Account.Create("user@example.com", "hashed", "Nick", "User", "Not/Valid", null, null, true);

        Assert.True(result.IsError);
        Assert.Equal("TimeZoneId.Invalid", result.FirstError.Code);
    }

    /// <summary>Create returns error, when role is not Admin or User.</summary>
    [Theory]
    [InlineData("Anonymous")]
    [InlineData("SuperAdmin")]
    [InlineData("")]
    public void Create_ReturnsError_WhenRoleInvalid(string role)
    {
        var result = Domain.Account.Account.Create("user@example.com", "hashed", "Nick", role, "UTC", null, null, true);

        Assert.True(result.IsError);
        Assert.Equal("Account.RoleInvalid", result.FirstError.Code);
    }

    // -- ConfirmEmail ---------------------------------------------------------

    /// <summary>Confirm email succeeds, when token matches.</summary>
    [Fact]
    public void ConfirmEmail_Succeeds_WhenTokenMatches()
    {
        string token = GuidToken.Generate();
        var account = ValidAccount(registrationToken: token);

        var result = account.ConfirmEmail(token);

        Assert.False(result.IsError);
        Assert.True(account.EmailConfirmed);
        Assert.Null(account.RegistrationToken);
    }

    /// <summary>Confirm email returns error, when already confirmed.</summary>
    [Fact]
    public void ConfirmEmail_ReturnsError_WhenAlreadyConfirmed()
    {
        string token = GuidToken.Generate();
        var account = ValidAccount(registrationToken: token);
        account.ConfirmEmail(token);

        var result = account.ConfirmEmail(token);

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

        var result = account.ConfirmEmail(token);

        Assert.True(result.IsError);
        Assert.Equal("Account.InvalidToken", result.FirstError.Code);
        Assert.False(account.EmailConfirmed);
    }

    /// <summary>Confirm email returns error, when token mismatch.</summary>
    [Fact]
    public void ConfirmEmail_ReturnsError_WhenTokenMismatch()
    {
        var account = ValidAccount(registrationToken: "correct");

        var result = account.ConfirmEmail("wrong");

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

        var result = account.ConfirmEmail(storedToken ?? "");

        Assert.True(result.IsError);
        Assert.Equal("Account.InvalidToken", result.FirstError.Code);
        Assert.False(account.EmailConfirmed);
    }

    // -- RequestPasswordReset -------------------------------------------------

    /// <summary>Request password reset succeeds, when email confirmed.</summary>
    [Fact]
    public void RequestPasswordReset_Succeeds_WhenEmailConfirmed()
    {
        string token = GuidToken.Generate();
        var account = ValidAccount(registrationToken: token);
        account.ConfirmEmail(token);

        var result = account.RequestPasswordReset("reset-token");

        Assert.False(result.IsError);
        Assert.Equal("reset-token", account.ResetPasswordToken);
    }

    /// <summary>Request password reset returns error, when email not confirmed.</summary>
    [Fact]
    public void RequestPasswordReset_ReturnsError_WhenEmailNotConfirmed()
    {
        var account = ValidAccount();

        var result = account.RequestPasswordReset("reset-token");

        Assert.True(result.IsError);
        Assert.Equal("Account.NotConfirmed", result.FirstError.Code);
    }

    /// <summary>Request password reset succeeds, when locked, since it is the way a locked account is recovered.</summary>
    [Fact]
    public void RequestPasswordReset_Succeeds_WhenLocked()
    {
        string token = GuidToken.Generate();
        var account = ValidAccount(registrationToken: token);
        account.ConfirmEmail(token);
        account.Lock("reason");

        var result = account.RequestPasswordReset("reset-token");

        Assert.False(result.IsError);
        Assert.Equal("reset-token", account.ResetPasswordToken);
    }

    // -- ResetPassword --------------------------------------------------------

    /// <summary>Reset password succeeds, when token valid.</summary>
    [Fact]
    public void ResetPassword_Succeeds_WhenTokenValid()
    {
        string registrationToken = GuidToken.Generate();
        var account = ValidAccount(registrationToken: registrationToken);
        account.ConfirmEmail(registrationToken);
        string resetToken = GuidToken.Generate();
        account.RequestPasswordReset(resetToken);

        var result = account.ResetPassword(resetToken, "newHash");

        Assert.False(result.IsError);
        Assert.Equal("newHash", account.HashedPassword);
        Assert.Null(account.ResetPasswordToken);
    }

    /// <summary>Reset password returns error, when token invalid.</summary>
    [Fact]
    public void ResetPassword_ReturnsError_WhenTokenInvalid()
    {
        string registrationToken = GuidToken.Generate();
        var account = ValidAccount(registrationToken: registrationToken);
        account.ConfirmEmail(registrationToken);
        account.RequestPasswordReset(GuidToken.Generate());

        var result = account.ResetPassword("wrong-tok", "newHash");

        Assert.True(result.IsError);
        Assert.Equal("Account.InvalidToken", result.FirstError.Code);
    }

    /// <summary>Reset password returns error, when the stored token has passed its 24h TTL, even
    /// though it still matches the candidate.</summary>
    [Fact]
    public void ResetPassword_ReturnsError_WhenTokenExpired()
    {
        string registrationToken = GuidToken.Generate();
        var account = ValidAccount(registrationToken: registrationToken);
        account.ConfirmEmail(registrationToken);
        string expiredResetToken = ExpiredToken();
        account.RequestPasswordReset(expiredResetToken);

        var result = account.ResetPassword(expiredResetToken, "newHash");

        Assert.True(result.IsError);
        Assert.Equal("Account.InvalidToken", result.FirstError.Code);
    }

    /// <summary>Reset password returns error, when new password empty.</summary>
    [Fact]
    public void ResetPassword_ReturnsError_WhenNewPasswordEmpty()
    {
        string registrationToken = GuidToken.Generate();
        var account = ValidAccount(registrationToken: registrationToken);
        account.ConfirmEmail(registrationToken);
        string resetToken = GuidToken.Generate();
        account.RequestPasswordReset(resetToken);

        var result = account.ResetPassword(resetToken, "");

        Assert.True(result.IsError);
        Assert.Equal("Account.PasswordEmpty", result.FirstError.Code);
    }

    // -- Login attempt tracking ------------------------------------------------

    /// <summary>Record login failure increments counter.</summary>
    [Fact]
    public void RecordLoginFailure_IncrementsCounter()
    {
        var account = ValidAccount();

        account.RecordLoginFailure(maxAttempts: 5);
        account.RecordLoginFailure(maxAttempts: 5);

        Assert.Equal(2, account.LoginAttempt);
        Assert.False(account.Locked);
    }

    /// <summary>Record login failure locks account, when max attempts reached.</summary>
    [Fact]
    public void RecordLoginFailure_LocksAccount_WhenMaxAttemptsReached()
    {
        var account = ValidAccount();

        account.RecordLoginFailure(maxAttempts: 3);
        account.RecordLoginFailure(maxAttempts: 3);
        account.RecordLoginFailure(maxAttempts: 3);

        Assert.Equal(3, account.LoginAttempt);
        Assert.True(account.Locked);
    }

    /// <summary>Record login failure locks account, when counter exceeds max.</summary>
    [Fact]
    public void RecordLoginFailure_LocksAccount_WhenCounterExceedsMax()
    {
        var account = ValidAccount();

        account.RecordLoginFailure(maxAttempts: 2);
        account.RecordLoginFailure(maxAttempts: 2);
        account.RecordLoginFailure(maxAttempts: 2);

        Assert.True(account.Locked);
    }

    /// <summary>Reset login attempt sets counter to zero.</summary>
    [Fact]
    public void ResetLoginAttempt_SetsCounterToZero()
    {
        var account = ValidAccount();
        account.RecordLoginFailure(maxAttempts: 5);
        account.RecordLoginFailure(maxAttempts: 5);

        account.ResetLoginAttempt();

        Assert.Equal(0, account.LoginAttempt);
    }

    // -- Lock / Unlock --------------------------------------------------------

    /// <summary>Lock sets locked true and stores message.</summary>
    [Fact]
    public void Lock_SetsLockedTrueAndStoresMessage()
    {
        var account = ValidAccount();

        account.Lock("spam");

        Assert.True(account.Locked);
        Assert.Equal("spam", account.Message);
    }

    /// <summary>Unlock sets locked false and resets attempts.</summary>
    [Fact]
    public void Unlock_SetsLockedFalseAndResetsAttempts()
    {
        var account = ValidAccount();
        account.RecordLoginFailure(maxAttempts: 5);
        account.Lock();

        account.Unlock();

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

        var result = account.ChangeRole(role);

        Assert.False(result.IsError);
        Assert.Equal(role, account.Role);
    }

    /// <summary>ChangeRole rejects any role other than Admin or User and keeps the current one.</summary>
    [Theory]
    [InlineData(Role.Anonymous)]
    [InlineData("Root")]
    [InlineData("")]
    public void ChangeRole_RejectsAnUnknownRole_AndKeepsTheCurrentOne(string role)
    {
        var account = ValidAccount();

        var result = account.ChangeRole(role);

        Assert.True(result.IsError);
        Assert.Equal("Account.RoleInvalid", result.FirstError.Code);
        Assert.Equal(Role.User, account.Role);
    }

    /// <summary>ChangeTimeZone sets a valid IANA zone.</summary>
    [Fact]
    public void ChangeTimeZone_SetsTheZone_WhenValid()
    {
        var account = ValidAccount();

        var result = account.ChangeTimeZone("Asia/Seoul");

        Assert.False(result.IsError);
        Assert.Equal("Asia/Seoul", account.TimeZone.Value);
    }

    /// <summary>ChangeTimeZone rejects an unknown zone and keeps the current one.</summary>
    [Fact]
    public void ChangeTimeZone_ReturnsError_WhenTimeZoneInvalid()
    {
        var account = ValidAccount();
        string before = account.TimeZone.Value;

        var result = account.ChangeTimeZone("Not/Valid");

        Assert.True(result.IsError);
        Assert.Equal("TimeZoneId.Invalid", result.FirstError.Code);
        Assert.Equal(before, account.TimeZone.Value);
    }

    /// <summary>RevokeEmailConfirmation marks a confirmed account unconfirmed again.</summary>
    [Fact]
    public void RevokeEmailConfirmation_ClearsTheConfirmedFlag()
    {
        var account = ValidAccount();
        account.ForceConfirmEmail();

        account.RevokeEmailConfirmation();

        Assert.False(account.EmailConfirmed);
    }

    /// <summary>RevokeServiceTerms clears the accepted-terms flag.</summary>
    [Fact]
    public void RevokeServiceTerms_ClearsTheAcceptedFlag()
    {
        var account = ValidAccount();
        account.AcceptServiceTerms();

        account.RevokeServiceTerms();

        Assert.False(account.AgreedServiceTerms);
    }

    /// <summary>Restore undoes a soft delete.</summary>
    [Fact]
    public void Restore_ClearsTheDeletedFlag()
    {
        var account = ValidAccount();
        account.SoftDelete();

        account.Restore();

        Assert.False(account.Deleted);
    }

    /// <summary>Lock keeps the failed-login counter; only Unlock resets it.</summary>
    [Fact]
    public void Lock_KeepsTheLoginAttemptCount()
    {
        var account = ValidAccount();
        account.RecordLoginFailure(maxAttempts: 5);
        account.RecordLoginFailure(maxAttempts: 5);

        account.Lock();

        Assert.Equal(2, account.LoginAttempt);
    }

    // -- SoftDelete -----------------------------------------------------------

    /// <summary>Soft delete sets deleted true.</summary>
    [Fact]
    public void SoftDelete_SetsDeletedTrue()
    {
        var account = ValidAccount();

        account.SoftDelete();

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
        string newToken = GuidToken.Generate();

        var result = account.ReplaceUnconfirmedRegistration("new-hash", "  Real   Owner ", "Asia/Seoul", newToken, agreedServiceTerms: true);

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
        string oldToken = GuidToken.Generate();
        string newToken = GuidToken.Generate();
        var account = ValidAccount(registrationToken: oldToken);

        account.ReplaceUnconfirmedRegistration("new-hash", "Owner", "UTC", newToken, agreedServiceTerms: true);

        Assert.True(account.ConfirmEmail(oldToken).IsError);
        Assert.False(account.ConfirmEmail(newToken).IsError);
    }

    /// <summary>A confirmed account's credentials can never be replaced by a registration.</summary>
    [Fact]
    public void ReplaceUnconfirmedRegistration_ReturnsConflict_AndChangesNothing_WhenAlreadyConfirmed()
    {
        string token = GuidToken.Generate();
        var account = ValidAccount(hashedPassword: "owner-hash", nickname: "Owner", registrationToken: token);
        account.ConfirmEmail(token);

        var result = account.ReplaceUnconfirmedRegistration("attacker-hash", "Attacker", "UTC", GuidToken.Generate(), agreedServiceTerms: true);

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
        string token = GuidToken.Generate();
        var account = ValidAccount(hashedPassword: "old-hash", nickname: "Squatter", registrationToken: token);

        var result = account.ReplaceUnconfirmedRegistration(hashedPassword, nickname, timeZone, GuidToken.Generate(), agreedServiceTerms: true);

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

        account.UpdateProfile("/avatar.png", newTz, "USD");

        Assert.Equal("/avatar.png", account.AvatarImagePath);
        Assert.Equal("Asia/Seoul", account.TimeZone.Value);
        Assert.Equal("USD", account.DefaultMonetaryUnit);
    }

    /// <summary>Update profile clears avatar and currency, when null.</summary>
    [Fact]
    public void UpdateProfile_ClearsAvatarAndCurrency_WhenNull()
    {
        var account = ValidAccount();
        var tz = TimeZoneId.Create("UTC").Value;
        account.UpdateProfile("/avatar.png", tz, "KRW");

        account.UpdateProfile(null, tz, null);

        Assert.Null(account.AvatarImagePath);
        Assert.Null(account.DefaultMonetaryUnit);
    }

    // -- ChangePassword -------------------------------------------------------

    /// <summary>Change password replaces hashed password.</summary>
    [Fact]
    public void ChangePassword_ReplacesHashedPassword()
    {
        var account = ValidAccount();

        account.ChangePassword("newHash");

        Assert.Equal("newHash", account.HashedPassword);
    }

    // -- SetMessage -----------------------------------------------------------

    /// <summary>Set message stores text.</summary>
    [Fact]
    public void SetMessage_StoresText()
    {
        var account = ValidAccount();

        account.SetMessage("Under review");

        Assert.Equal("Under review", account.Message);
    }

    /// <summary>Set message clears message, when null.</summary>
    [Fact]
    public void SetMessage_ClearsMessage_WhenNull()
    {
        var account = ValidAccount();
        account.SetMessage("some message");

        account.SetMessage(null);

        Assert.Null(account.Message);
    }

    // -- ForceConfirmEmail ----------------------------------------------------

    /// <summary>Force confirm email confirms email and clears token.</summary>
    [Fact]
    public void ForceConfirmEmail_ConfirmsEmailAndClearsToken()
    {
        var account = ValidAccount(registrationToken: "tok");

        account.ForceConfirmEmail();

        Assert.True(account.EmailConfirmed);
        Assert.Null(account.RegistrationToken);
    }

    /// <summary>Force confirm email is idempotent, when already confirmed.</summary>
    [Fact]
    public void ForceConfirmEmail_IsIdempotent_WhenAlreadyConfirmed()
    {
        var account = ValidAccount(registrationToken: "tok");
        account.ForceConfirmEmail();

        account.ForceConfirmEmail();

        Assert.True(account.EmailConfirmed);
    }

    // -- AcceptServiceTerms ---------------------------------------------------

    /// <summary>Accept service terms sets agreed to true.</summary>
    [Fact]
    public void AcceptServiceTerms_SetsAgreedToTrue()
    {
        var account = ValidAccount(agreedServiceTerms: false);

        account.AcceptServiceTerms();

        Assert.True(account.AgreedServiceTerms);
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

        account.ChangePassword("newHash");

        Assert.NotEqual(originalStamp, account.SecurityStamp);
    }

    /// <summary>Self-service change password clears a pending forced password change.</summary>
    [Fact]
    public void ChangePassword_ClearsMustChangePassword()
    {
        var account = ValidAccount();
        account.AdminResetPassword("tempHash");

        account.ChangePassword("newHash");

        Assert.False(account.MustChangePassword);
    }

    /// <summary>Admin reset password replaces the hash, forces a password change, and regenerates the security stamp.</summary>
    [Fact]
    public void AdminResetPassword_SetsHashForcesChange_AndRegeneratesSecurityStamp()
    {
        var account = ValidAccount();
        string originalStamp = account.SecurityStamp;

        account.AdminResetPassword("tempHash");

        Assert.Equal("tempHash", account.HashedPassword);
        Assert.True(account.MustChangePassword);
        Assert.NotEqual(originalStamp, account.SecurityStamp);
    }

    /// <summary>Self-service change password discards a pending reset token, so an earlier reset link cannot overwrite the new password.</summary>
    [Fact]
    public void ChangePassword_ClearsPendingResetPasswordToken()
    {
        string registrationToken = GuidToken.Generate();
        var account = ValidAccount(registrationToken: registrationToken);
        account.ConfirmEmail(registrationToken);
        string resetToken = GuidToken.Generate();
        account.RequestPasswordReset(resetToken);

        account.ChangePassword("newHash");

        Assert.Null(account.ResetPasswordToken);
        Assert.True(account.ResetPassword(resetToken, "attackerHash").IsError);
        Assert.Equal("newHash", account.HashedPassword);
    }

    /// <summary>Admin reset discards a pending reset token, so an earlier reset link cannot bypass the forced password change.</summary>
    [Fact]
    public void AdminResetPassword_ClearsPendingResetPasswordToken()
    {
        string registrationToken = GuidToken.Generate();
        var account = ValidAccount(registrationToken: registrationToken);
        account.ConfirmEmail(registrationToken);
        string resetToken = GuidToken.Generate();
        account.RequestPasswordReset(resetToken);

        account.AdminResetPassword("tempHash");

        Assert.Null(account.ResetPasswordToken);
        Assert.True(account.ResetPassword(resetToken, "attackerHash").IsError);
        Assert.Equal("tempHash", account.HashedPassword);
        Assert.True(account.MustChangePassword);
    }

    /// <summary>Token-based password reset regenerates the security stamp and clears a pending forced password change.</summary>
    [Fact]
    public void ResetPassword_RegeneratesSecurityStamp_AndClearsMustChangePassword()
    {
        string registrationToken = GuidToken.Generate();
        var account = ValidAccount(registrationToken: registrationToken);
        account.ConfirmEmail(registrationToken);
        // The admin reset comes first: it discards any reset link requested before it, so the
        // link used below must be requested afterward.
        account.AdminResetPassword("tempHash");
        string resetToken = GuidToken.Generate();
        account.RequestPasswordReset(resetToken);
        string stampBeforeReset = account.SecurityStamp;

        var result = account.ResetPassword(resetToken, "newHash");

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

        account.RegenerateRegistrationToken("fresh-token");

        Assert.Equal("fresh-token", account.RegistrationToken);
    }

    /// <summary>Once the e-mail is confirmed there is nothing left to confirm, so regenerating changes nothing.</summary>
    [Fact]
    public void RegenerateRegistrationToken_IsANoOp_OnceConfirmed()
    {
        string token = GuidToken.Generate();
        var account = ValidAccount(registrationToken: token);
        account.ConfirmEmail(token);

        account.RegenerateRegistrationToken("fresh-token");

        Assert.Null(account.RegistrationToken);
    }
}
