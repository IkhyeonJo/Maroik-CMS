using Maroik.Core.Domain.Calendar;
namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Repository abstraction over <see cref="CalendarShared"/> (sharing-permission) persistence.
/// </summary>
public interface ICalendarSharedRepository : IGenericRepository<CalendarShared>
{
    /// <summary>Returns the sharing-permission rows for all calendars.</summary>
    Task<List<CalendarShared>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns the sharing-permission rows for the given calendar ids, locked with
    /// SELECT ... FOR UPDATE. Must be called inside an active unit-of-work transaction; serializes
    /// against a concurrent write to the same rows (e.g. UpdateCalendarSharedAsync unsharing one of
    /// these calendars) so a caller cannot act on a permission snapshot a concurrent change is about
    /// to invalidate.
    /// </summary>
    Task<List<CalendarShared>> GetByIdsForUpdateAsync(IEnumerable<long> calendarIds, CancellationToken ct = default);
}
