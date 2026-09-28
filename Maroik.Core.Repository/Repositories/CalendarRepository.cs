using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Calendar;
using Maroik.Core.PostgreSQL.Data;
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

    /// <inheritdoc />
    public Task<List<Calendar>> GetAllOrderedByNameAsync(CancellationToken ct = default)
        => QueryAllAsync(q => q.OrderBy(e => e.Name), ct: ct);

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
