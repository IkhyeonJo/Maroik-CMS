using Maroik.Core.Domain.Calendar;
namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Repository abstraction over <see cref="CalendarEventReminder"/> persistence.
/// </summary>
public interface ICalendarEventReminderRepository : IGenericRepository<CalendarEventReminder>
{
    /// <summary>Returns all reminders configured for the given calendar event.</summary>
    Task<List<CalendarEventReminder>> GetByCalendarEventIdAsync(long calendarEventId, CancellationToken ct = default);

    /// <summary>Deletes all reminders for the given calendar event.</summary>
    Task DeleteByCalendarEventIdAsync(long calendarEventId, CancellationToken ct = default);
}
