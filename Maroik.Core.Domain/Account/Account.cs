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
    /// <summary>Admin-facing note set on an account when it is locked out after too many failed login attempts.</summary>
    private const string AccountLockedMessage = "This account is locked";

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

    /// <summary>Account role: "Admin" or "User".</summary>
    public string Role { get; private set; }

    /// <summary>IANA time-zone used for date/time display.</summary>
    public TimeZoneId TimeZone { get; private set; }

    /// <summary>Default currency code for the personal account-book (e.g. "KRW", "USD"). Null when no currency has been configured.</summary>
    public string? DefaultMonetaryUnit { get; private set; }

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
        string role,
        TimeZoneId timeZone,
        string? defaultMonetaryUnit,
        string? registrationToken,
        bool agreedServiceTerms) : base(email.Value)
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
        Created = DateTime.UtcNow;
        Updated = DateTime.UtcNow;
    }

    /// <summary>Reconstitution constructor: assigns every field verbatim from trusted storage with
    /// no "new entity" side effects (no fresh <see cref="SecurityStamp"/>, no <see cref="DateTime.UtcNow"/>).</summary>
    private Account(
        Email email, string hashedPassword, string nickname, string? avatarImagePath, string role,
        TimeZoneId timeZone, string? defaultMonetaryUnit, bool locked, long loginAttempt, bool emailConfirmed,
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
        bool mustChangePassword)
    {
        return new Account(
            Email.FromTrustedSource(email),
            hashedPassword,
            nickname,
            avatarImagePath,
            role,
            TimeZoneId.FromTrustedSource(timeZoneIanaId),
            defaultMonetaryUnit,
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
            mustChangePassword);
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
        string role,
        string timeZoneValue,
        string? defaultMonetaryUnit,
        string? registrationToken,
        bool agreedServiceTerms,
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

        if (role is not (Maroik.Core.Domain.Account.Role.Admin or Maroik.Core.Domain.Account.Role.User))
            return LocalizableError.Validation("Account.RoleInvalid", "Role must be either Admin or User.");

        var account = new Account(
            emailResult.Value, hashedPassword, nickname, role,
            tzResult.Value, defaultMonetaryUnit, registrationToken, agreedServiceTerms);

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
    public ErrorOr<Success> ConfirmEmail(string token)
    {
        if (EmailConfirmed)
            return LocalizableError.Conflict("Account.AlreadyConfirmed", "Email address is already confirmed.");

        if (!TokensMatch(RegistrationToken, token) || !GuidToken.IsTokenAlive(RegistrationToken!))
            return LocalizableError.Validation("Account.InvalidToken", "Invalid email confirmation token.");

        EmailConfirmed = true;
        RegistrationToken = null;
        Updated = DateTime.UtcNow;
        return Result.Success;
    }

    /// <summary>
    /// Stores a password-reset token
    /// so an email can be dispatched by the application layer.
    /// Available even when the account is locked, since resetting the password
    /// is the intended way for a user to recover from a login lockout.
    /// </summary>
    public ErrorOr<Success> RequestPasswordReset(string resetToken)
    {
        if (!EmailConfirmed)
            return LocalizableError.Failure("Account.NotConfirmed", "Email must be confirmed before resetting the password.");

        ResetPasswordToken = resetToken;
        Updated = DateTime.UtcNow;
        return Result.Success;
    }

    /// <summary>
    /// Validates the reset token — must match <see cref="ResetPasswordToken"/> and still be within
    /// <see cref="GuidToken"/>'s 24 h window — and replaces the hashed password. Expiry is enforced
    /// here, not just by callers, so this invariant can't be silently skipped by a future caller
    /// that forgets to pre-check <see cref="GuidToken.IsTokenAlive"/> itself.
    /// </summary>
    public ErrorOr<Success> ResetPassword(string token, string newHashedPassword)
    {
        if (!TokensMatch(ResetPasswordToken, token) || !GuidToken.IsTokenAlive(ResetPasswordToken!))
            return LocalizableError.Validation("Account.InvalidToken", "Invalid or expired password-reset token.");

        if (string.IsNullOrWhiteSpace(newHashedPassword))
            return LocalizableError.Validation("Account.PasswordEmpty", "New hashed password cannot be empty.");

        HashedPassword = newHashedPassword;
        ResetPasswordToken = null;
        SecurityStamp = GenerateSecurityStamp();
        MustChangePassword = false;
        Updated = DateTime.UtcNow;
        return Result.Success;
    }

    /// <summary>
    /// Increments the failed-login counter and locks the account when <paramref name="maxAttempts"/> is reached.
    /// </summary>
    public void RecordLoginFailure(int maxAttempts)
    {
        LoginAttempt++;
        Updated = DateTime.UtcNow;
        if (LoginAttempt >= maxAttempts)
            Lock(AccountLockedMessage);
    }

    /// <summary>Resets the failed-login counter to zero (called after a successful login).</summary>
    public void ResetLoginAttempt()
    {
        LoginAttempt = 0;
        Updated = DateTime.UtcNow;
    }

    /// <summary>
    /// Locks the account, preventing login.
    /// </summary>
    public void Lock(string? message = null)
    {
        Locked = true;
        Message = message;
        Updated = DateTime.UtcNow;
    }

    /// <summary>Unlocks the account and resets the login-attempt counter.</summary>
    public void Unlock()
    {
        Locked = false;
        LoginAttempt = 0;
        Message = null;
        Updated = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the account's profile fields. Nickname is not included — it is set once at
    /// registration and never changed afterward (the profile edit UI shows it read-only).
    /// </summary>
    public void UpdateProfile(string? avatarImagePath, TimeZoneId timeZone, string? defaultMonetaryUnit)
    {
        AvatarImagePath = avatarImagePath;
        TimeZone = timeZone;
        DefaultMonetaryUnit = defaultMonetaryUnit;
        Updated = DateTime.UtcNow;
    }

    /// <summary>
    /// Replaces the stored BCrypt hash for a self-service password change. Regenerates
    /// <see cref="SecurityStamp"/> (invalidating any other open session on its next request) and
    /// clears <see cref="MustChangePassword"/>, since supplying a new hash here always goes through
    /// the password-policy-validated self-service flow. Also discards any pending
    /// <see cref="ResetPasswordToken"/>: a reset link mailed before this change must not stay
    /// usable to overwrite the password the account owner just chose.
    /// </summary>
    public void ChangePassword(string newHashedPassword)
    {
        HashedPassword = newHashedPassword;
        ResetPasswordToken = null;
        SecurityStamp = GenerateSecurityStamp();
        MustChangePassword = false;
        Updated = DateTime.UtcNow;
    }

    /// <summary>
    /// Admin override: replaces the stored BCrypt hash and forces the account to choose its own
    /// new password (meeting <see cref="PasswordPolicy"/>) the next time it logs in. Regenerates
    /// <see cref="SecurityStamp"/> so any session already open under the old password is
    /// invalidated on its next request, and discards any pending <see cref="ResetPasswordToken"/>
    /// so a reset link mailed earlier cannot be used to bypass the forced change.
    /// </summary>
    public void AdminResetPassword(string newHashedPassword)
    {
        HashedPassword = newHashedPassword;
        ResetPasswordToken = null;
        SecurityStamp = GenerateSecurityStamp();
        MustChangePassword = true;
        Updated = DateTime.UtcNow;
    }

    /// <summary>Marks the account as deleted without removing the database row.</summary>
    public void SoftDelete()
    {
        Deleted = true;
        Updated = DateTime.UtcNow;
    }

    /// <summary>Sets the administrative notification message (e.g. lock reason, status text).</summary>
    public void SetMessage(string? message)
    {
        Message = message;
        Updated = DateTime.UtcNow;
    }

    /// <summary>
    /// Assigns a new registration token only when the current token is null or empty.
    /// Used in the resend-confirmation flow when the original token was cleared.
    /// </summary>
    public void RegenerateRegistrationTokenIfEmpty(string newToken)
    {
        if (!string.IsNullOrEmpty(RegistrationToken))
        {
            return;
        }

        RegistrationToken = newToken;
        Updated = DateTime.UtcNow;
    }

    /// <summary>
    /// Unconditionally replaces the registration token — the resend / re-register flow calls this
    /// when the stored token is missing <em>or expired</em>, so a stale, un-confirmable registration
    /// can always be recovered by requesting a fresh email. No-op once the email is confirmed
    /// (there is nothing left to confirm).
    /// </summary>
    public void RegenerateRegistrationToken(string newToken)
    {
        if (EmailConfirmed)
        {
            return;
        }

        RegistrationToken = newToken;
        Updated = DateTime.UtcNow;
    }

    /// <summary>
    /// Admin bypass: confirms the email without token validation.
    /// Use only when an admin creates an account that should be immediately active.
    /// </summary>
    public void ForceConfirmEmail()
    {
        EmailConfirmed = true;
        RegistrationToken = null;
        Updated = DateTime.UtcNow;
    }

    /// <summary>
    /// Ensures the service-terms acceptance flag is set to true.
    /// Used when a returning user tries to register again on an already-confirmed account.
    /// </summary>
    public void AcceptServiceTerms()
    {
        AgreedServiceTerms = true;
        Updated = DateTime.UtcNow;
    }

    /// <summary>
    /// Admin-level composite update. Validates and replaces the time-zone;
    /// resets the login counter whenever the account is unlocked.
    /// </summary>
    public ErrorOr<Success> AdminUpdate(
        string? role,
        string? timeZoneIanaId,
        bool locked,
        long loginAttempt,
        bool emailConfirmed,
        bool agreedServiceTerms,
        string? message,
        bool deleted)
    {
        var tzResult = TimeZoneId.Create(timeZoneIanaId ?? TimeZone.Value);
        if (tzResult.IsError) return tzResult.Errors;

        if (role is not (null or Maroik.Core.Domain.Account.Role.Admin or Maroik.Core.Domain.Account.Role.User))
            return LocalizableError.Validation("Account.RoleInvalid", "Role must be either Admin or User.");

        Role = role ?? Role;
        TimeZone = tzResult.Value;
        Locked = locked;
        LoginAttempt = locked ? loginAttempt : 0;
        EmailConfirmed = emailConfirmed;
        AgreedServiceTerms = agreedServiceTerms;
        Message = message;
        Deleted = deleted;
        Updated = DateTime.UtcNow;
        return Result.Success;
    }
}
