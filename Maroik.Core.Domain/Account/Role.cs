namespace Maroik.Core.Domain.Account;

/// <summary>
/// String constants for the three access roles used throughout the application.
/// Store and compare role values using these constants to avoid magic strings.
/// </summary>
public static class Role
{
    /// <summary>Full administrative access. Can manage accounts, menus, and all board content.</summary>
    public const string Admin = "Admin";

    /// <summary>Standard authenticated user. Can access personal account-book, calendar, and forum features.</summary>
    public const string User = "User";

    /// <summary>Unauthenticated visitor. Read-only access to publicly shared content.</summary>
    public const string Anonymous = "Anonymous";
}
