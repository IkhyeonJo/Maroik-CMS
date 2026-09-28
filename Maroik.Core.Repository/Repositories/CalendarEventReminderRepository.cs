using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Calendar;
using Maroik.Core.PostgreSQL.Data;
using Microsoft.EntityFrameworkCore;
using OrmReminder = Maroik.Core.PostgreSQL.Models.CalendarEventReminder;

namespace Maroik.Core.Repository.Repositories;

/// <summary>
/// EF Core repository for <see cref="CalendarEventReminder"/> domain objects.
/// </summary>
public class CalendarEventReminderRepository(ApplicationDbContext context)
    : GenericRepository<CalendarEventReminder, OrmReminder>(context), ICalendarEventReminderRepository
{
    /// <summary>The underlying <see cref="DbSet{TEntity}"/> for <see cref="OrmReminder"/> rows.</summary>
    protected override DbSet<OrmReminder> Set => Context.CalendarEventReminders;

    /// <summary>Maps a persisted <see cref="OrmReminder"/> row to the <see cref="CalendarEventReminder"/> domain object.</summary>
    protected override CalendarEventReminder ToDomain(OrmReminder e) => CalendarEventReminder.Reconstitute(
        e.Id, e.CalendarEventId, e.Method,
        e.MinutesBeforeEvent, e.HoursBeforeEvent,
        e.DaysBeforeEvent, e.WeeksBeforeEvent, e.TimesBeforeEvent);

    /// <summary>Maps a <see cref="CalendarEventReminder"/> domain object to its <see cref="OrmReminder"/> persistence representation.</summary>
    protected override OrmReminder ToEntity(CalendarEventReminder r) => new()
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
