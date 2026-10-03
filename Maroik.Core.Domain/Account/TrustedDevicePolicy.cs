namespace Maroik.Core.Domain.Account;

/// <summary>
/// The rules of a trusted device: a browser that has signed in to an account successfully and holds that account's
/// device cookie. Such a browser is not held off by <see cref="LoginThrottlePolicy"/>, so someone guessing wrong on
/// purpose cannot stop the owner from signing in on the devices they already use.
/// </summary>
public static class TrustedDevicePolicy
{
    /// <summary>How long a device stays trusted after the sign-in that made it so.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(180);

    /// <summary>
    /// Consecutive failed logins from trusted devices after which every device of the account stops being trusted
    /// (so a stolen device cookie cannot be used for unlimited guessing).
    /// </summary>
    public const int MaxFailedAttempts = 5;
}
