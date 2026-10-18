namespace Maroik.Website.Tests.Fixtures;

/// <summary>
/// Password hashes for the accounts the tests seed straight into the database. BCrypt keeps its cost in the hash, and the
/// site verifies against whatever cost a stored hash carries, so a seeded account logs in through the real
/// <c>PasswordService.VerifyPassword</c> either way. Production's cost 13 takes about 0.5 s to hash and as long again to
/// verify, which the hundreds of seeded logins in this suite paid for minutes; cost 4 (BCrypt's minimum) takes under a
/// millisecond. Passwords the site hashes itself (registration, reset, change) still use the production cost.
/// </summary>
internal static class TestPasswords
{
    /// <summary>BCrypt cost for seeded test accounts.</summary>
    private const int SeedWorkFactor = 4;

    /// <summary>A BCrypt hash of <paramref name="password"/> at <see cref="SeedWorkFactor"/>.</summary>
    public static string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, SeedWorkFactor);
}
