using Maroik.Core.Domain.Calendar;
namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Repository abstraction over <see cref="OtherCalendar"/> (subscribed shared-calendar) persistence.
/// </summary>
public interface IOtherCalendarRepository : IGenericRepository<OtherCalendar>
{
    /// <summary>Returns the calendars that the given account subscribes to from other users.</summary>
    Task<List<OtherCalendar>> GetByAccountEmailAsync(string email, CancellationToken ct = default);

    /// <summary>Returns all other-calendar subscription rows.</summary>
    Task<List<OtherCalendar>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Deletes all subscription rows for the given account.</summary>
    Task DeleteByAccountEmailAsync(string email, CancellationToken ct = default);

    /// <summary>Deletes every subscription row for the given calendar, across all subscribing accounts, in one statement.</summary>
    Task DeleteByCalendarIdAsync(long calendarId, CancellationToken ct = default);
}
