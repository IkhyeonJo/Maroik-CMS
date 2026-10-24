using Maroik.Core.Domain.Calendars;
namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Repository abstraction over <see cref="CalendarEventReminder"/> persistence.
/// </summary>
public interface ICalendarEventReminderRepository : IGenericRepository<CalendarEventReminder>
{
    /// <summary>Returns all reminders configured for the given calendar event.</summary>
    Task<List<CalendarEventReminder>> GetByCalendarEventIdAsync(long calendarEventId, CancellationToken ct = default);

    /// <summary>
    /// Locks and returns up to <paramref name="limit"/> unhandled reminders due by <paramref name="nowUtc"/>, oldest first. Must run
    /// inside a transaction; rows another transaction holds are skipped, so two workers never take the same reminder.
    /// </summary>
    Task<List<CalendarEventReminder>> LockDueAsync(DateTime nowUtc, int limit, CancellationToken ct = default);

    /// <summary>Deletes all reminders for the given calendar event.</summary>
    Task DeleteByCalendarEventIdAsync(long calendarEventId, CancellationToken ct = default);
}
