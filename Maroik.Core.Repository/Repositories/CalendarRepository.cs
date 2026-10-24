using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Calendars;
using Maroik.Core.PostgreSQL.Contexts;
using Microsoft.EntityFrameworkCore;
using OrmCalendar = Maroik.Core.PostgreSQL.Models.Calendar;

namespace Maroik.Core.Repository.Repositories;

/// <summary>
/// EF Core repository for <see cref="Calendar"/> domain objects.
/// Maps color codes and IANA time zone IDs between domain value objects and plain strings.
/// </summary>
public class CalendarRepository(ApplicationDbContext context)
    : GenericRepository<Calendar, OrmCalendar>(context), ICalendarRepository
{
    /// <summary>The underlying <see cref="DbSet{TEntity}"/> for <see cref="OrmCalendar"/> rows.</summary>
    protected override DbSet<OrmCalendar> Set => Context.Calendars;

    /// <summary>Maps a persisted <see cref="OrmCalendar"/> row to the <see cref="Calendar"/> domain object.</summary>
    protected override Calendar ToDomain(OrmCalendar e) => Calendar.Reconstitute(
        e.Id, e.AccountEmail, e.Name,
        e.Description, e.TimeZoneIanaId, e.HtmlColorCode,
        e.Created, e.Updated);

    /// <summary>Maps a <see cref="Calendar"/> domain object to its <see cref="OrmCalendar"/> persistence representation.</summary>
    protected override OrmCalendar ToEntity(Calendar c) => new()
    {
        Id = c.Id,
        AccountEmail = c.AccountEmail.Value,
        Name = c.Name,
        Description = c.Description,
        TimeZoneIanaId = c.TimeZone.Value,
        HtmlColorCode = c.ColorCode.Value,
        Created = c.Created,
        Updated = c.Updated
    };

    /// <inheritdoc />
    public Task<List<Calendar>> GetByAccountEmailAsync(string accountEmail, CancellationToken ct = default)
        => QueryAsync(e => e.AccountEmail == accountEmail, ct: ct);

    // The three sharing reads below are display reads (never saved back): one query each over the rows that qualify, untracked,
    // so a page that lists shared calendars no longer loads every account's calendars and every sharing row to filter in memory.

    /// <inheritdoc />
    public Task<List<Calendar>> GetSharedWithGuestsAsync(CancellationToken ct = default)
        => ReadAsync(Set.Where(c => Context.CalendarShareds.Any(s => s.CalendarId == c.Id && s.Anonymous)), ct);

    /// <inheritdoc />
    public Task<List<Calendar>> GetSharedWithUsersAsync(CancellationToken ct = default)
        => ReadAsync(Set.Where(c => Context.CalendarShareds.Any(s => s.CalendarId == c.Id && s.User)), ct);

    /// <inheritdoc />
    public Task<List<Calendar>> GetSubscribedByAsync(string accountEmail, CancellationToken ct = default)
        => ReadAsync(Set.Where(c =>
            Context.OtherCalendars.Any(o => o.CalendarId == c.Id && o.AccountEmail == accountEmail)
            && Context.CalendarShareds.Any(s => s.CalendarId == c.Id && s.User)), ct);

    /// <summary>Runs <paramref name="query"/> untracked and maps the rows to <see cref="Calendar"/>s.</summary>
    private async Task<List<Calendar>> ReadAsync(IQueryable<OrmCalendar> query, CancellationToken ct)
        => [.. (await query.AsNoTracking().ToListAsync(ct)).Select(ToDomain)];

    /// <inheritdoc />
    public Task<Calendar?> FindByIdAsync(long id, CancellationToken ct = default)
        => QueryFirstAsync(e => e.Id == id, ct: ct);

    /// <inheritdoc />
    public async Task<Calendar?> FindByIdForUpdateAsync(long id, CancellationToken ct = default)
    {
        // Raw SQL is required because EF Core LINQ cannot emit FOR UPDATE. AsNoTracking so a
        // pre-lock instance already tracked in this context can't snap back and defeat the lock
        // (see BoardRepository.FindActiveByIdForUpdateAsync for the same rationale).
        var entities = await Set.FromSqlInterpolated(
            $"""
             SELECT *
             FROM "Calendar"
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
