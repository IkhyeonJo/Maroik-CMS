namespace Maroik.Core.Domain.Account;

/// <summary>
/// Policy deciding which roles are shown local (time-zone-converted) timestamps versus raw UTC.
/// Authenticated roles (Admin, User) have a known, trustworthy time zone on their account; an
/// Anonymous viewer does not, so timestamps shown to them stay in UTC. Kept as an explicit Domain
/// policy — rather than repeated inline in every view that formats a timestamp — so the rule has a
/// single, testable home and callers (Contract DTOs, controllers, views) only ever consume the
/// resulting bool.
/// </summary>
public static class AccountViewPolicy
{
    /// <summary>True when an account with this <paramref name="role"/> should see local time instead of raw UTC.</summary>
    public static bool SeesLocalTime(string? role) => role is Role.Admin or Role.User;
}
