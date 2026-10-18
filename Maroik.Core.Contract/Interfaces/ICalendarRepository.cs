using Maroik.Core.Domain.Calendars;
namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Repository abstraction over <see cref="Calendar"/> persistence.
/// </summary>
public interface ICalendarRepository : IGenericRepository<Calendar>
{
    /// <summary>Returns calendars owned by the given account.</summary>
    Task<List<Calendar>> GetByAccountEmailAsync(string accountEmail, CancellationToken ct = default);

    /// <summary>
    /// Returns the calendar with the given ID, row-locked (<c>FOR UPDATE</c>) for the rest of the
    /// caller's transaction, or <see langword="null"/> if it doesn't exist. Callers must still
    /// verify ownership (<c>AccountEmail</c>) themselves — this does not filter by account.
    /// </summary>
    Task<Calendar?> FindByIdForUpdateAsync(long id, CancellationToken ct = default);

    /// <summary>Returns the calendars shared with guests (<c>CalendarShared.Anonymous</c>): what a signed-out visitor sees.</summary>
    Task<List<Calendar>> GetSharedWithGuestsAsync(CancellationToken ct = default);

    /// <summary>Returns the calendars shared with registered users (<c>CalendarShared.User</c>): what an account may subscribe to.</summary>
    Task<List<Calendar>> GetSharedWithUsersAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns the calendars <paramref name="accountEmail"/> subscribes to (<c>OtherCalendar</c>) that are still shared with users —
    /// a subscription to a calendar since turned private is left out even if its row was not cleaned up.
    /// </summary>
    Task<List<Calendar>> GetSubscribedByAsync(string accountEmail, CancellationToken ct = default);

    /// <summary>Deletes the calendar with the given ID.</summary>
    Task DeleteByIdAsync(long id, CancellationToken ct = default);
}
