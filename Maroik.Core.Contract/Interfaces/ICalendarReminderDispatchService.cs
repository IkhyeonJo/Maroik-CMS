namespace Maroik.Core.Contract.Interfaces;

/// <summary>Sends the calendar reminders that are due (run every minute by the worker's <c>CalendarReminderScheduler</c>).</summary>
public interface ICalendarReminderDispatchService
{
    /// <summary>
    /// Handles the due reminders one at a time, each in its own transaction: e-mails it to its calendar's owner through the mail
    /// queue, or skips it (see <c>ReminderDeliveryPolicy</c>), and marks it handled. Stops when none is due, after
    /// <paramref name="max"/> reminders, or at the first failure (the reminder stays due and is tried again on the next run).
    /// Returns how many were handled.
    /// </summary>
    Task<int> DispatchDueAsync(int max, CancellationToken ct = default);
}
