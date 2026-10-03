using System.Security.Cryptography;
using System.Text;
using ErrorOr;
using Maroik.Core.Domain.Localization;
using Maroik.Core.Domain.Primitives;
using Maroik.Core.Domain.ValueObjects;

namespace Maroik.Core.Domain.Account;

/// <summary>
/// Aggregate root for user accounts.
/// All state changes are performed through domain methods that enforce business rules.
/// </summary>
public sealed class Account : AggregateRoot<string>
{
    /// <summary>
    /// Relative path stored in <see cref="AvatarImagePath"/> for an account that has not uploaded
    /// its own avatar. Single source of truth for the value the persistence and presentation layers
    /// would otherwise each hard-code.
    /// </summary>
    public const string DefaultAvatarImagePath = "/upload/Management/Profile/default-avatar.jpg";

    /// <summary>Validated email address (also serves as the primary key).</summary>
    public Email Email { get; private set; }

    /// <summary>BCrypt-hashed password — never exposed as plain text.</summary>
    public string HashedPassword { get; private set; }

    /// <summary>Unique display name shown throughout the UI.</summary>
    public string Nickname { get; private set; }

    /// <summary>Relative path to the user's avatar image.</summary>
    public string? AvatarImagePath { get; private set; }

    /// <summary>Account role: Admin or User.</summary>
    public AccountRole Role { get; private set; }

    /// <summary>IANA time-zone used for date/time display.</summary>
    public TimeZoneId TimeZone { get; private set; }

    /// <summary>Default currency code for the personal account-book (e.g. "KRW", "USD"). Null when no currency has been configured.</summary>
    public CurrencyCode? DefaultMonetaryUnit { get; private set; }

    /// <summary>When true the account cannot log in.</summary>
    public bool Locked { get; private set; }

    /// <summary>Number of consecutive failed login attempts.</summary>
    public long LoginAttempt { get; private set; }

    /// <summary>Whether the registration email has been confirmed.</summary>
    public bool EmailConfirmed { get; private set; }

    /// <summary>Whether the user accepted the service terms of use.</summary>
    public bool AgreedServiceTerms { get; private set; }

    /// <summary>Token embedded in the registration confirmation email (valid 24 h). Null after confirmation.</summary>
    public string? RegistrationToken { get; private set; }

    /// <summary>Token embedded in the password-reset email (valid 24 h). Null when no reset is pending.</summary>
    public string? ResetPasswordToken { get; private set; }

    /// <summary>UTC timestamp when the account was created.</summary>
    public DateTime Created { get; private set; }

    /// <summary>UTC timestamp of the most recent update.</summary>
    public DateTime Updated { get; private set; }

    /// <summary>Optional admin-facing note (used for lock reason, etc.).</summary>
    public string? Message { get; private set; }

    /// <summary>Soft-delete flag — the row is kept but the account is treated as deleted.</summary>
    public bool Deleted { get; private set; }

    /// <summary>
    /// Opaque value that changes every time the password changes (self-service, forgot-password
    /// reset, or admin override). The Website layer captures this in the session at login and
    /// invalidates the session on its next request once it no longer matches the stored value.
    /// </summary>
    public string SecurityStamp { get; private set; }

    /// <summary>
    /// When true, the account must set a new password (meeting <see cref="PasswordPolicy"/>) before
    /// it can do anything else. Set by <see cref="AdminResetPassword"/>; cleared by any successful
    /// password change.
    /// </summary>
    public bool MustChangePassword { get; private set; }

    /// <summary>
    /// Until when new logins are held off after failed attempts (<see cref="LoginThrottlePolicy"/>), or
    /// <see langword="null"/> when they are not. Cleared by a successful login, a password reset and an unlock.
    /// </summary>
    public DateTime? LoginBlockedUntil { get; private set; }

    /// <summary>
    /// Opaque value every trusted-device cookie of this account carries (<see cref="TrustedDevicePolicy"/>). Replacing
    /// it stops every device of the account from being trusted at once.
    /// </summary>
    public string DeviceStamp { get; private set; } = "";

    /// <summary>Consecutive failed logins made from trusted devices (see <see cref="TrustedDevicePolicy.MaxFailedAttempts"/>).</summary>
    public long TrustedDeviceLoginAttempt { get; private set; }

    /// <summary>
    /// True when a device cookie carrying <paramref name="deviceStamp"/>, issued at <paramref name="issuedAt"/>, still
    /// makes its browser a trusted device of this account at <paramref name="utcNow"/>.
    /// </summary>
    public bool TrustsDevice(string? deviceStamp, DateTime issuedAt, DateTime utcNow) =>
        TokensMatch(DeviceStamp, deviceStamp)
        && issuedAt <= utcNow
        && utcNow - issuedAt < TrustedDevicePolicy.Lifetime;

    /// <summary>
    /// Records a failed login made from a trusted device. At <see cref="TrustedDevicePolicy.MaxFailedAttempts"/> such
    /// failures every device stops being trusted (a new <see cref="DeviceStamp"/>); returns whether that happened.
    /// The account's own wait (<see cref="LoginBlockedUntil"/>) is not touched.
    /// </summary>
    public bool RecordTrustedDeviceLoginFailure(DateTime utcNow)
    {
        TrustedDeviceLoginAttempt++;
        Updated = utcNow;
        if (TrustedDeviceLoginAttempt < TrustedDevicePolicy.MaxFailedAttempts)
            return false;

        DeviceStamp = GenerateSecurityStamp();
        TrustedDeviceLoginAttempt = 0;
        return true;
    }

    /// <summary>True while <see cref="LoginBlockedUntil"/> is still ahead of <paramref name="utcNow"/>.</summary>
    public bool IsLoginBlocked(DateTime utcNow) => LoginBlockedUntil > utcNow;

    /// <summary>Generates a fresh, unpredictable security stamp value.</summary>
    private static string GenerateSecurityStamp() => Guid.NewGuid().ToString("N");

    /// <summary>
    /// Constant-time comparison of a bearer token (registration/password-reset) against the value
    /// stored on this account. Guards against a timing side-channel that a plain <c>==</c>/<c>!=</c>
    /// comparison (which short-circuits on the first differing byte) would leak on these
    /// account-takeover-adjacent tokens. Null/empty on either side never matches.
    /// </summary>
    private static bool TokensMatch(string? stored, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(stored) || string.IsNullOrWhiteSpace(candidate))
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(stored), Encoding.UTF8.GetBytes(candidate));
    }

    /// <summary>"Brand-new account" constructor: stamps <see cref="Created"/>/<see cref="Updated"/>
    /// and a fresh <see cref="SecurityStamp"/>. Used by <see cref="Create"/> only.</summary>
    private Account(
        Email email,
        string hashedPassword,
        string nickname,
        AccountRole role,
        TimeZoneId timeZone,
        CurrencyCode? defaultMonetaryUnit,
        string? registrationToken,
        bool agreedServiceTerms,
        DateTime utcNow) : base(email.Value)
    {
        Email = email;
        HashedPassword = hashedPassword;
        Nickname = nickname;
        Role = role;
        TimeZone = timeZone;
        DefaultMonetaryUnit = defaultMonetaryUnit;
        RegistrationToken = registrationToken;
        AgreedServiceTerms = agreedServiceTerms;
        SecurityStamp = GenerateSecurityStamp();
        DeviceStamp = GenerateSecurityStamp();
        Created = utcNow;
        Updated = utcNow;
    }

    /// <summary>Reconstitution constructor: assigns every field verbatim from trusted storage with
    /// no "new entity" side effects (no fresh <see cref="SecurityStamp"/>, no new timestamps).</summary>
    private Account(
        Email email, string hashedPassword, string nickname, string? avatarImagePath, AccountRole role,
        TimeZoneId timeZone, CurrencyCode? defaultMonetaryUnit, bool locked, long loginAttempt, bool emailConfirmed,
        bool agreedServiceTerms, string? registrationToken, string? resetPasswordToken, DateTime created,
        DateTime updated, string? message, bool deleted, string securityStamp, bool mustChangePassword) : base(email.Value)
    {
        Email = email;
        HashedPassword = hashedPassword;
        Nickname = nickname;
        AvatarImagePath = avatarImagePath;
        Role = role;
        TimeZone = timeZone;
        DefaultMonetaryUnit = defaultMonetaryUnit;
        Locked = locked;
        LoginAttempt = loginAttempt;
        EmailConfirmed = emailConfirmed;
        AgreedServiceTerms = agreedServiceTerms;
        RegistrationToken = registrationToken;
        ResetPasswordToken = resetPasswordToken;
        Created = created;
        Updated = updated;
        Message = message;
        Deleted = deleted;
        SecurityStamp = securityStamp;
        MustChangePassword = mustChangePassword;
    }

    // ------------------------------------------------------------------------
    // Factory / Reconstitution
    // ------------------------------------------------------------------------

    /// <summary>
    /// Rebuilds an <see cref="Account"/> from trusted raw values from the repository layer.
    /// Bypasses validation — use only with data from the trusted repository layer.
    /// </summary>
    public static Account Reconstitute(
        string email,
        string hashedPassword,
        string nickname,
        string? avatarImagePath,
        string role,
        string timeZoneIanaId,
        string? defaultMonetaryUnit,
        bool locked,
        long loginAttempt,
        bool emailConfirmed,
        bool agreedServiceTerms,
        string? registrationToken,
        string? resetPasswordToken,
        DateTime created,
        DateTime updated,
        string? message,
        bool deleted,
        string securityStamp,
        bool mustChangePassword,
        DateTime? loginBlockedUntil = null,
        string deviceStamp = "",
        long trustedDeviceLoginAttempt = 0)
    {
        return new Account(
            Email.FromTrustedSource(email),
            hashedPassword,
            nickname,
            avatarImagePath,
            AccountRole.FromTrustedSource(role),
            TimeZoneId.FromTrustedSource(timeZoneIanaId),
            defaultMonetaryUnit == null ? null : CurrencyCode.FromTrustedSource(defaultMonetaryUnit),
            locked,
            loginAttempt,
            emailConfirmed,
            agreedServiceTerms,
            registrationToken,
            resetPasswordToken,
            created,
            updated,
            message,
            deleted,
            securityStamp,
            mustChangePassword)
        {
            LoginBlockedUntil = loginBlockedUntil,
            DeviceStamp = deviceStamp,
            TrustedDeviceLoginAttempt = trustedDeviceLoginAttempt
        };
    }

    /// <summary>
    /// Creates a new <see cref="Account"/>. The nickname is validated and normalized by
    /// <see cref="NicknamePolicy"/>; <paramref name="allowReservedNickname"/> lets an administrator
    /// creating an account use a reserved name (self-registration leaves it <see langword="false"/>).
    /// </summary>
    public static ErrorOr<Account> Create(
        string emailValue,
        string hashedPassword,
        string nickname,
        AccountRole role,
        string timeZoneValue,
        CurrencyCode? defaultMonetaryUnit,
        string? registrationToken,
        bool agreedServiceTerms,
        DateTime utcNow,
        bool allowReservedNickname = false)
    {
        var emailResult = Email.Create(emailValue);
        if (emailResult.IsError) return emailResult.Errors;

        var tzResult = TimeZoneId.Create(timeZoneValue);
        if (tzResult.IsError) return tzResult.Errors;

        // The nickname is stored in its normalized form (see NicknamePolicy), never as typed.
        var nicknameResult = NicknamePolicy.Validate(nickname, allowReservedNickname);
        if (nicknameResult.IsError) return nicknameResult.Errors;
        nickname = nicknameResult.Value;

        if (string.IsNullOrWhiteSpace(hashedPassword))
            return LocalizableError.Validation("Account.PasswordEmpty", "Hashed password cannot be empty.");

        var account = new Account(
            emailResult.Value, hashedPassword, nickname, role,
            tzResult.Value, defaultMonetaryUnit, registrationToken, agreedServiceTerms, utcNow);

        return account;
    }

    // ------------------------------------------------------------------------
    // Domain behaviours
    // ------------------------------------------------------------------------

    /// <summary>
    /// Validates the registration token — must match <see cref="RegistrationToken"/> and still be
    /// within <see cref="GuidToken"/>'s 24 h window — and activates the account. Expiry is enforced
    /// here, not just by callers, so this invariant can't be silently skipped by a future caller
    /// that forgets to pre-check <see cref="GuidToken.IsTokenAlive"/> itself.
    /// </summary>
    public ErrorOr<Success> ConfirmEmail(string token, DateTime utcNow)
    {
        if (EmailConfirmed)
            return LocalizableError.Conflict("Account.AlreadyConfirmed", "Email address is already confirmed.");

        if (!TokensMatch(RegistrationToken, token) || !GuidToken.IsTokenAlive(RegistrationToken!, utcNow))
            return LocalizableError.Validation("Account.InvalidToken", "Invalid email confirmation token.");

        EmailConfirmed = true;
        RegistrationToken = null;
        Updated = utcNow;
        return Result.Success;
    }

    /// <summary>
    /// Stores a password-reset token
    /// so an email can be dispatched by the application layer.
    /// Available even when the account is locked, since resetting the password
    /// is the intended way for a user to recover from a login lockout.
    /// </summary>
    public ErrorOr<Success> RequestPasswordReset(string resetToken, DateTime utcNow)
    {
        if (!EmailConfirmed)
            return LocalizableError.Failure("Account.NotConfirmed", "Email must be confirmed before resetting the password.");

        ResetPasswordToken = resetToken;
        Updated = utcNow;
        return Result.Success;
    }

    /// <summary>
    /// Validates the reset token — must match <see cref="ResetPasswordToken"/> and still be within
    /// <see cref="GuidToken"/>'s 24 h window — and replaces the hashed password. Expiry is enforced
    /// here, not just by callers, so this invariant can't be silently skipped by a future caller
    /// that forgets to pre-check <see cref="GuidToken.IsTokenAlive"/> itself.
    /// </summary>
    public ErrorOr<Success> ResetPassword(string token, string newHashedPassword, DateTime utcNow)
    {
        if (!TokensMatch(ResetPasswordToken, token) || !GuidToken.IsTokenAlive(ResetPasswordToken!, utcNow))
            return LocalizableError.Validation("Account.InvalidToken", "Invalid or expired password-reset token.");

        if (string.IsNullOrWhiteSpace(newHashedPassword))
            return LocalizableError.Validation("Account.PasswordEmpty", "New hashed password cannot be empty.");

        HashedPassword = newHashedPassword;
        ResetPasswordToken = null;
        SecurityStamp = GenerateSecurityStamp();
        MustChangePassword = false;
        Updated = utcNow;
        return Result.Success;
    }

    /// <summary>
    /// Increments the failed-login counter and, once it reaches a stage of <see cref="LoginThrottlePolicy"/>
    /// (<paramref name="maxAttempts"/> failures per stage), holds new logins off for that stage's wait, counted from
    /// <paramref name="utcNow"/>. The account is never locked by failures: the wait ends by itself.
    /// </summary>
    public void RecordLoginFailure(int maxAttempts, DateTime utcNow)
    {
        LoginAttempt++;
        Updated = utcNow;
        if (LoginThrottlePolicy.DelayAfter(LoginAttempt, maxAttempts) is { } delay)
            LoginBlockedUntil = utcNow + delay;
    }

    /// <summary>Resets the failed-login counter to zero and ends any wait (called after a successful login).</summary>
    public void ResetLoginAttempt(DateTime utcNow)
    {
        LoginAttempt = 0;
        LoginBlockedUntil = null;
        TrustedDeviceLoginAttempt = 0;
        Updated = utcNow;
    }

    /// <summary>
    /// Locks the account, preventing login.
    /// </summary>
    public void Lock(DateTime utcNow, string? message = null)
    {
        Locked = true;
        Message = message;
        Updated = utcNow;
    }

    /// <summary>
    /// An administrator's lock: locks the account like <see cref="Lock"/> and also replaces the
    /// <see cref="SecurityStamp"/>, so every session the account has open ends on its next request. A lock from
    /// failed logins (<see cref="RecordLoginFailure"/>) keeps the stamp — it only refuses new logins — so someone
    /// who guesses wrong on purpose cannot throw the owner out of the site.
    /// </summary>
    public void LockAndEndSessions(DateTime utcNow)
    {
        Lock(utcNow);
        SecurityStamp = GenerateSecurityStamp();
    }

    /// <summary>Unlocks the account and resets the login-attempt counter.</summary>
    public void Unlock(DateTime utcNow)
    {
        Locked = false;
        LoginAttempt = 0;
        LoginBlockedUntil = null;
        Message = null;
        Updated = utcNow;
    }

    /// <summary>
    /// Updates the account's profile fields. Nickname is not included — it is set at registration
    /// and never changed afterward (the profile edit UI shows it read-only); the only exception is
    /// <see cref="ReplaceUnconfirmedRegistration"/>, before the registration is confirmed.
    /// </summary>
    public void UpdateProfile(string? avatarImagePath, TimeZoneId timeZone, CurrencyCode? defaultMonetaryUnit, DateTime utcNow)
    {
        AvatarImagePath = avatarImagePath;
        TimeZone = timeZone;
        DefaultMonetaryUnit = defaultMonetaryUnit;
        Updated = utcNow;
    }

    /// <summary>
    /// Replaces the stored BCrypt hash for a self-service password change. Regenerates
    /// <see cref="SecurityStamp"/> (invalidating any other open session on its next request) and
    /// clears <see cref="MustChangePassword"/>, since supplying a new hash here always goes through
    /// the password-policy-validated self-service flow. Also discards any pending
    /// <see cref="ResetPasswordToken"/>: a reset link mailed before this change must not stay
    /// usable to overwrite the password the account owner just chose. An empty or blank hash is
    /// rejected and nothing changes.
    /// </summary>
    public ErrorOr<Success> ChangePassword(string newHashedPassword, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(newHashedPassword))
            return LocalizableError.Validation("Account.PasswordEmpty", "New hashed password cannot be empty.");

        HashedPassword = newHashedPassword;
        ResetPasswordToken = null;
        SecurityStamp = GenerateSecurityStamp();
        MustChangePassword = false;
        Updated = utcNow;
        return Result.Success;
    }

    /// <summary>
    /// Admin override: replaces the stored BCrypt hash and forces the account to choose its own
    /// new password (meeting <see cref="PasswordPolicy"/>) the next time it logs in. Regenerates
    /// <see cref="SecurityStamp"/> so any session already open under the old password is
    /// invalidated on its next request, and discards any pending <see cref="ResetPasswordToken"/>
    /// so a reset link mailed earlier cannot be used to bypass the forced change. An empty or blank
    /// hash is rejected and nothing changes.
    /// </summary>
    public ErrorOr<Success> AdminResetPassword(string newHashedPassword, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(newHashedPassword))
            return LocalizableError.Validation("Account.PasswordEmpty", "New hashed password cannot be empty.");

        HashedPassword = newHashedPassword;
        ResetPasswordToken = null;
        SecurityStamp = GenerateSecurityStamp();
        MustChangePassword = true;
        Updated = utcNow;
        return Result.Success;
    }

    /// <summary>Marks the account as deleted without removing the database row.</summary>
    public void SoftDelete(DateTime utcNow)
    {
        Deleted = true;
        Updated = utcNow;
    }

    /// <summary>Sets the administrative notification message (e.g. lock reason, status text).</summary>
    public void SetMessage(string? message, DateTime utcNow)
    {
        Message = message;
        Updated = utcNow;
    }

    /// <summary>
    /// Replaces the credentials of a registration nobody has confirmed yet — the re-register path.
    /// Until the email is confirmed no one has proven they own the address, so the latest
    /// registrant's password, nickname, time zone and terms acceptance win, and the fresh
    /// <paramref name="registrationToken"/> invalidates every confirmation link mailed earlier.
    /// This is the one place a nickname changes after <see cref="Create"/>: an unconfirmed account
    /// has not finished registering. Refused once the email is confirmed.
    /// </summary>
    public ErrorOr<Success> ReplaceUnconfirmedRegistration(
        string hashedPassword, string nickname, string timeZoneValue, string registrationToken, bool agreedServiceTerms, DateTime utcNow)
    {
        if (EmailConfirmed)
            return LocalizableError.Conflict("Account.AlreadyConfirmed", "Email address is already confirmed.");

        var nicknameResult = NicknamePolicy.Validate(nickname);
        if (nicknameResult.IsError) return nicknameResult.Errors;

        var tzResult = TimeZoneId.Create(timeZoneValue);
        if (tzResult.IsError) return tzResult.Errors;

        if (string.IsNullOrWhiteSpace(hashedPassword))
            return LocalizableError.Validation("Account.PasswordEmpty", "Hashed password cannot be empty.");

        HashedPassword = hashedPassword;
        Nickname = nicknameResult.Value;
        TimeZone = tzResult.Value;
        AgreedServiceTerms = agreedServiceTerms;
        RegistrationToken = registrationToken;
        SecurityStamp = GenerateSecurityStamp();
        Updated = utcNow;
        return Result.Success;
    }

    /// <summary>
    /// Unconditionally replaces the registration token — the resend / re-register flow calls this
    /// when the stored token is missing <em>or expired</em>, so a stale, un-confirmable registration
    /// can always be recovered by requesting a fresh email. No-op once the email is confirmed
    /// (there is nothing left to confirm).
    /// </summary>
    public void RegenerateRegistrationToken(string newToken, DateTime utcNow)
    {
        if (EmailConfirmed)
        {
            return;
        }

        RegistrationToken = newToken;
        Updated = utcNow;
    }

    /// <summary>
    /// Admin bypass: confirms the email without token validation.
    /// Use only when an admin creates an account that should be immediately active.
    /// </summary>
    public void ForceConfirmEmail(DateTime utcNow)
    {
        EmailConfirmed = true;
        RegistrationToken = null;
        Updated = utcNow;
    }

    /// <summary>
    /// Ensures the service-terms acceptance flag is set to true.
    /// Used when a returning user tries to register again on an already-confirmed account.
    /// </summary>
    public void AcceptServiceTerms(DateTime utcNow)
    {
        AgreedServiceTerms = true;
        Updated = utcNow;
    }

    /// <summary>Administrator operation: changes the account's role.</summary>
    public void ChangeRole(AccountRole role, DateTime utcNow)
    {
        Role = role;
        Updated = utcNow;
    }

    /// <summary>Administrator operation: changes the account's display time zone.</summary>
    public ErrorOr<Success> ChangeTimeZone(string timeZoneIanaId, DateTime utcNow)
    {
        var tzResult = TimeZoneId.Create(timeZoneIanaId);
        if (tzResult.IsError) return tzResult.Errors;

        TimeZone = tzResult.Value;
        Updated = utcNow;
        return Result.Success;
    }

    /// <summary>
    /// Administrator operation: marks the email address as not confirmed again. The account cannot
    /// log in until it is confirmed (by an administrator, or through a freshly requested
    /// confirmation mail).
    /// </summary>
    public void RevokeEmailConfirmation(DateTime utcNow)
    {
        EmailConfirmed = false;
        Updated = utcNow;
    }

    /// <summary>Administrator operation: withdraws the account's acceptance of the service terms.</summary>
    public void RevokeServiceTerms(DateTime utcNow)
    {
        AgreedServiceTerms = false;
        Updated = utcNow;
    }

    /// <summary>Administrator operation: undoes <see cref="SoftDelete"/>.</summary>
    public void Restore(DateTime utcNow)
    {
        Deleted = false;
        Updated = utcNow;
    }
}
