using Maroik.Core.Domain.Calendar;
namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Repository abstraction over <see cref="CalendarEventAttachedFile"/> persistence.
/// </summary>
public interface ICalendarEventAttachedFileRepository : IGenericRepository<CalendarEventAttachedFile>
{
    /// <summary>Returns the file attached to the given calendar event, or null if none exists.</summary>
    Task<CalendarEventAttachedFile?> FindByCalendarEventIdAsync(long calendarEventId, CancellationToken ct = default);

    /// <summary>Deletes the attached file for the given calendar event, if any.</summary>
    Task DeleteByCalendarEventIdAsync(long calendarEventId, CancellationToken ct = default);
}
