using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Calendar;
using Maroik.Core.PostgreSQL.Data;
using Microsoft.EntityFrameworkCore;
using OrmCalendarEventReminder = Maroik.Core.PostgreSQL.Models.CalendarEventReminder;

namespace Maroik.Core.Repository.Repositories;

/// <summary>
/// EF Core repository for <see cref="CalendarEventReminder"/> domain objects.
/// </summary>
public class CalendarEventReminderRepository(ApplicationDbContext context)
    : GenericRepository<CalendarEventReminder, OrmCalendarEventReminder>(context), ICalendarEventReminderRepository
{
    /// <summary>The underlying <see cref="DbSet{TEntity}"/> for <see cref="OrmCalendarEventReminder"/> rows.</summary>
    protected override DbSet<OrmCalendarEventReminder> Set => Context.CalendarEventReminders;

    /// <summary>Maps a persisted <see cref="OrmCalendarEventReminder"/> row to the <see cref="CalendarEventReminder"/> domain object.</summary>
    protected override CalendarEventReminder ToDomain(OrmCalendarEventReminder e) => CalendarEventReminder.Reconstitute(
        e.Id, e.CalendarEventId, e.Method,
        e.MinutesBeforeEvent, e.HoursBeforeEvent,
        e.DaysBeforeEvent, e.WeeksBeforeEvent, e.TimesBeforeEvent);

    /// <summary>Maps a <see cref="CalendarEventReminder"/> domain object to its <see cref="OrmCalendarEventReminder"/> persistence representation.</summary>
    protected override OrmCalendarEventReminder ToEntity(CalendarEventReminder r) => new()
    {
        Id = r.Id,
        CalendarEventId = r.CalendarEventId,
        Method = r.Method,
        MinutesBeforeEvent = r.MinutesBeforeEvent,
        HoursBeforeEvent = r.HoursBeforeEvent,
        DaysBeforeEvent = r.DaysBeforeEvent,
        WeeksBeforeEvent = r.WeeksBeforeEvent,
        TimesBeforeEvent = r.TimesBeforeEvent
    };

    /// <inheritdoc />
    public Task<List<CalendarEventReminder>> GetByCalendarEventIdAsync(long calendarEventId, CancellationToken ct = default)
        => QueryAsync(e => e.CalendarEventId == calendarEventId, ct: ct);

    /// <inheritdoc />
    public Task DeleteByCalendarEventIdAsync(long calendarEventId, CancellationToken ct = default)
        => DeleteWhereAsync(e => e.CalendarEventId == calendarEventId, ct);
}
