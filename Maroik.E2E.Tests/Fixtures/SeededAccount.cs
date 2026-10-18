
namespace Maroik.E2E.Tests.Fixtures;

/// <summary>An account seeded straight into the E2E database, ready to sign in through the UI.</summary>
/// <param name="Email">Sign-in e-mail.</param>
/// <param name="Password">Plain-text password (meets the password policy).</param>
/// <param name="Nickname">Unique nickname.</param>
public sealed record SeededAccount(string Email, string Password, string Nickname);
