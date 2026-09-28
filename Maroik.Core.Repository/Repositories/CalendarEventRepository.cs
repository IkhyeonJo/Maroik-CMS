using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Calendar;
using Maroik.Core.PostgreSQL.Data;
using Microsoft.EntityFrameworkCore;
using OrmCalendarEvent = Maroik.Core.PostgreSQL.Models.CalendarEvent;

namespace Maroik.Core.Repository.Repositories;

/// <summary>
/// EF Core repository for <see cref="CalendarEvent"/> domain objects.
/// Returns the database-generated ID when creating a new event.
/// </summary>
public class CalendarEventRepository(ApplicationDbContext context)
    : GenericRepository<CalendarEvent, OrmCalendarEvent>(context), ICalendarEventRepository
{
    /// <summary>The underlying <see cref="DbSet{TEntity}"/> for <see cref="OrmCalendarEvent"/> rows.</summary>
    protected override DbSet<OrmCalendarEvent> Set => Context.CalendarEvents;

    /// <inheritdoc />
    public async Task<long> CreateCalendarEventAsync(CalendarEvent calendarEvent, CancellationToken ct = default)
    {
        OrmCalendarEvent entity = ToEntity(calendarEvent);
        _ = await Context.CalendarEvents.AddAsync(entity, ct);
        // Flushed unconditionally (unlike the deferred CreateAsync): the caller needs the
        // database-generated ID for the reminder / attached-file rows it writes next in the same
        // unit of work. The INSERT stays uncommitted until the transaction commits.
        _ = await Context.SaveChangesAsync(ct);
        return entity.Id;
    }

    /// <summary>Maps a persisted <see cref="OrmCalendarEvent"/> row to the <see cref="CalendarEvent"/> domain object.</summary>
    protected override CalendarEvent ToDomain(OrmCalendarEvent e) => CalendarEvent.Reconstitute(
        e.Id, e.CalendarId, e.Title, e.Description,
        e.AllDay, e.StartDate, e.EndDate,
        e.StartDateTimeZoneIanaId, e.EndDateTimeZoneIanaId,
        e.Location, e.Status, e.RecurrenceId, e.Created, e.Updated);

    /// <summary>Maps a <see cref="CalendarEvent"/> domain object to its <see cref="OrmCalendarEvent"/> persistence representation.</summary>
    protected override OrmCalendarEvent ToEntity(CalendarEvent ce) => new()
    {
        Id = ce.Id,
        CalendarId = ce.CalendarId,
        Title = ce.Title,
        Description = ce.Description,
        AllDay = ce.AllDay,
        StartDate = ce.StartDate,
        EndDate = ce.EndDate,
        StartDateTimeZoneIanaId = ce.StartDateTimeZoneIanaId,
        EndDateTimeZoneIanaId = ce.EndDateTimeZoneIanaId,
        Location = ce.Location,
        // A blank status (the domain accepts null/whitespace as "not specified") persists as the
        // "Busy" default: CalendarEvent_Status_check only allows the known values, so whitespace
        // would otherwise be rejected by the database instead of defaulted.
        Status = string.IsNullOrWhiteSpace(ce.Status) ? "Busy" : ce.Status,
        RecurrenceId = ce.RecurrenceId,
        Created = ce.Created,
        Updated = ce.Updated
    };

    /// <inheritdoc />
    public Task<List<CalendarEvent>> GetByCalendarIdsAsync(IEnumerable<long> calendarIds, CancellationToken ct = default)
    {
        var ids = calendarIds.ToList();
        return QueryAsync(e => ids.Contains(e.CalendarId), ct: ct);
    }

    /// <inheritdoc />
    public Task<CalendarEvent?> FindByIdAsync(long id, CancellationToken ct = default)
        => QueryFirstAsync(e => e.Id == id, ct: ct);

    /// <inheritdoc />
    public async Task<CalendarEvent?> FindByIdForUpdateAsync(long id, CancellationToken ct = default)
    {
        // Raw SQL is required because EF Core LINQ cannot emit FOR UPDATE. AsNoTracking so a
        // pre-lock instance already tracked in this context can't snap back and defeat the lock
        // (see BoardRepository.FindActiveByIdForUpdateAsync for the same rationale).
        var entities = await Set.FromSqlInterpolated(
            $"""
             SELECT *
             FROM "CalendarEvent"
             WHERE "Id" = {id}
             LIMIT 1
             FOR UPDATE
             """).AsNoTracking().ToListAsync(ct);

        var entity = entities.FirstOrDefault();
        return entity == null ? null : ToDomain(entity);
    }

    /// <inheritdoc />
    public Task DeleteByIdAsync(long id, CancellationToken ct = default)
        => DeleteWhereAsync(e => e.Id == id, ct);
}
