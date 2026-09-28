using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Calendar;
using Maroik.Core.PostgreSQL.Data;
using Microsoft.EntityFrameworkCore;
using OrmCalendarShared = Maroik.Core.PostgreSQL.Models.CalendarShared;

namespace Maroik.Core.Repository.Repositories;

/// <summary>
/// EF Core repository for <see cref="CalendarShared"/> domain objects,
/// which control whether a calendar is visible to authenticated users or anonymous visitors.
/// </summary>
public class CalendarSharedRepository(ApplicationDbContext context)
    : GenericRepository<CalendarShared, OrmCalendarShared>(context), ICalendarSharedRepository
{
    /// <summary>The underlying <see cref="DbSet{TEntity}"/> for <see cref="OrmCalendarShared"/> rows.</summary>
    protected override DbSet<OrmCalendarShared> Set => Context.CalendarShareds;

    /// <summary>Maps a persisted <see cref="OrmCalendarShared"/> row to the <see cref="CalendarShared"/> domain object.</summary>
    protected override CalendarShared ToDomain(OrmCalendarShared e)
        => CalendarShared.Reconstitute(e.CalendarId, e.User, e.Anonymous);

    /// <summary>Maps a <see cref="CalendarShared"/> domain object to its <see cref="OrmCalendarShared"/> persistence representation.</summary>
    protected override OrmCalendarShared ToEntity(CalendarShared s) => new()
    {
        CalendarId = s.Id,
        User = s.User,
        Anonymous = s.Anonymous
    };

    /// <inheritdoc />
    public Task<List<CalendarShared>> GetAllAsync(CancellationToken ct = default)
        => QueryAllAsync(ct: ct);

    /// <inheritdoc />
    public async Task<List<CalendarShared>> GetByIdsForUpdateAsync(IEnumerable<long> calendarIds, CancellationToken ct = default)
    {
        long[] ids = [.. calendarIds];
        if (ids.Length == 0) return [];

        // Raw SQL is required because EF Core LINQ cannot emit FOR UPDATE. AsNoTracking so a
        // pre-lock instance already tracked in this context can't snap back and defeat the lock
        // (see BoardRepository.FindActiveByIdForUpdateAsync for the same rationale).
        // ORDER BY "CalendarId" locks every matched row in a fixed, deterministic order regardless
        // of the order ids were passed in — the same ORDER BY + FOR UPDATE technique
        // AssetRepository.FindByEmailAndProductNamesForUpdateAsync uses to guarantee two concurrent
        // batched-lock callers with overlapping id sets can never acquire their locks in different
        // orders and deadlock.
        var entities = await Set.FromSqlInterpolated(
            $"""
             SELECT *
             FROM "CalendarShared"
             WHERE "CalendarId" = ANY({ids})
             ORDER BY "CalendarId"
             FOR UPDATE
             """).AsNoTracking().ToListAsync(ct);

        return [.. entities.Select(ToDomain)];
    }
}
