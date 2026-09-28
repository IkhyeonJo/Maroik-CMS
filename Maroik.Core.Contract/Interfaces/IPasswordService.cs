namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Service interface for password hashing and verification using BCrypt.
/// Never store or compare plain-text passwords; always use these methods.
/// </summary>
public interface IPasswordService
{
    /// <summary>
    /// Hashes a plain-text password using BCrypt.
    /// </summary>
    /// <param name="password">The plain-text password to hash.</param>
    /// <returns>A BCrypt-hashed password string safe for database storage.</returns>
    public string HashPassword(string password);

    /// <summary>
    /// Verifies that a plain-text password matches a stored BCrypt hash.
    /// </summary>
    /// <param name="password">The plain-text password entered by the user.</param>
    /// <param name="hashedPassword">The stored BCrypt hash to compare against.</param>
    /// <returns>True if the password matches; otherwise false.</returns>
    public bool VerifyPassword(string password, string hashedPassword);

    /// <summary>
    /// Runs a full password verification against a fixed internal dummy hash and always returns
    /// <see langword="false"/>. Call this on an "account not found" login path so its response time
    /// matches the "wrong password" path — otherwise an attacker can enumerate valid emails by
    /// timing (a missing account skips the ~2^work-factor BCrypt work entirely).
    /// </summary>
    public bool VerifyDummy(string password);
}
