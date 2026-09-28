using Maroik.Core.Domain.Calendar;
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

    /// <summary>Returns every calendar in the system, ordered by name.</summary>
    Task<List<Calendar>> GetAllOrderedByNameAsync(CancellationToken ct = default);

    /// <summary>Deletes the calendar with the given ID.</summary>
    Task DeleteByIdAsync(long id, CancellationToken ct = default);
}
