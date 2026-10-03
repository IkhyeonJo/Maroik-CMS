namespace Maroik.Core.Domain.Account;

/// <summary>
/// How long an account's logins are held off after failed attempts. The wait grows with the number of
/// consecutive failures and always ends by itself — there is no permanent lock — so guessing wrong on purpose
/// can delay an account's logins but never shut its owner out until a password reset.
/// </summary>
public static class LoginThrottlePolicy
{
    /// <summary>
    /// The wait after <paramref name="failedAttempts"/> consecutive failed logins, or <see langword="null"/> when
    /// there is none yet. <paramref name="step"/> is the configured attempt count per stage.
    /// </summary>
    public static TimeSpan? DelayAfter(long failedAttempts, int step)
    {
        long stage = failedAttempts / Math.Max(1, step);
        if (stage <= 0)
            return null;
        return StageDelays[(int)Math.Min(stage, StageDelays.Length) - 1];
    }

    /// <summary>
    /// Least time between two alert mails to the same account ("several sign-ins failed"), so failed logins cannot
    /// be used to flood its mailbox.
    /// </summary>
    public static readonly TimeSpan AlertInterval = TimeSpan.FromHours(24);

    /// <summary>The wait for the 1st, 2nd, 3rd and (capped) 4th-and-later stage.</summary>
    private static readonly TimeSpan[] StageDelays =
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromHours(1)];
}
