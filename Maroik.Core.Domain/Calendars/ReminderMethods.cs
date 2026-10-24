namespace Maroik.Core.Domain.Calendars;

/// <summary>
/// The delivery method stored verbatim in <see cref="CalendarEventReminder.Method"/>. A reminder is always an e-mail (sent by the
/// worker's <c>CalendarReminderScheduler</c>); the DB's <c>CalendarEventReminder_Method_check</c> constraint allows this value only.
/// </summary>
public static class ReminderMethods
{
    /// <summary>Deliver the reminder by e-mail.</summary>
    public const string Email = "Email";
}
