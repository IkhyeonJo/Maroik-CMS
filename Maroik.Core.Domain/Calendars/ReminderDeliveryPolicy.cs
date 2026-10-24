namespace Maroik.Core.Domain.Calendars;

/// <summary>
/// Whether a due calendar reminder is e-mailed to its calendar's owner. It is skipped when the owner no longer exists, is deleted or
/// locked, has not confirmed the e-mail address, or is the public demo account (anyone can add reminders to it, and it would mail
/// whatever address it carries again and again); and when it is more than <see cref="MaxLateness"/> late (the worker was down), as
/// a reminder that arrives after the event is noise. Viewers of a shared calendar never get one.
/// </summary>
public static class ReminderDeliveryPolicy
{
    /// <summary>How late a reminder may still be sent.</summary>
    public static readonly TimeSpan MaxLateness = TimeSpan.FromHours(1);

    /// <summary>
    /// Null when the reminder due at <paramref name="fireAtUtc"/> is to be sent to <paramref name="owner"/> at
    /// <paramref name="utcNow"/>; otherwise why it is skipped (logged by the scheduler).
    /// </summary>
    public static string? SkipReason(ReminderRecipient? owner, string? demoAccountEmail, DateTime fireAtUtc, DateTime utcNow)
    {
        if (owner is null) return "the calendar's owner no longer exists";
        if (owner.Deleted) return "the owner's account is deleted";
        if (owner.Locked) return "the owner's account is locked";
        if (!owner.EmailConfirmed) return "the owner has not confirmed the e-mail address";
        if (string.Equals(owner.Email, demoAccountEmail, StringComparison.OrdinalIgnoreCase)) return "the owner is the public demo account";
        return utcNow - fireAtUtc > MaxLateness ? "it is more than an hour late" : null;
    }
}
