using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Calendar;
using Maroik.Core.PostgreSQL.Data;
using Microsoft.EntityFrameworkCore;
using OrmOtherCalendar = Maroik.Core.PostgreSQL.Models.OtherCalendar;

namespace Maroik.Core.Repository.Repositories;

/// <summary>
/// EF Core repository for <see cref="OtherCalendar"/> domain objects,
/// which link an account to calendars they have subscribed to but do not own.
/// </summary>
public class OtherCalendarRepository(ApplicationDbContext context)
    : GenericRepository<OtherCalendar, OrmOtherCalendar>(context), IOtherCalendarRepository
{
    /// <summary>The underlying <see cref="DbSet{TEntity}"/> for <see cref="OrmOtherCalendar"/> rows.</summary>
    protected override DbSet<OrmOtherCalendar> Set => Context.OtherCalendars;

    /// <summary>Maps a persisted <see cref="OrmOtherCalendar"/> row to the <see cref="OtherCalendar"/> domain object.</summary>
    protected override OtherCalendar ToDomain(OrmOtherCalendar e)
        => OtherCalendar.Reconstitute(e.AccountEmail ?? "", e.CalendarId);

    /// <summary>Maps an <see cref="OtherCalendar"/> domain object to its <see cref="OrmOtherCalendar"/> persistence representation.</summary>
    protected override OrmOtherCalendar ToEntity(OtherCalendar o) => new()
    {
        AccountEmail = o.AccountEmail.Value,
        CalendarId = o.CalendarId
    };

    /// <inheritdoc />
    public Task<List<OtherCalendar>> GetByAccountEmailAsync(string email, CancellationToken ct = default)
        => QueryAsync(e => e.AccountEmail == email, ct: ct);

    /// <inheritdoc />
    public Task<List<OtherCalendar>> GetAllAsync(CancellationToken ct = default)
        => QueryAllAsync(ct: ct);

    /// <inheritdoc />
    public Task DeleteByAccountEmailAsync(string email, CancellationToken ct = default)
        => DeleteWhereAsync(e => e.AccountEmail == email, ct);

    /// <inheritdoc />
    public Task DeleteByCalendarIdAsync(long calendarId, CancellationToken ct = default)
        => DeleteWhereAsync(e => e.CalendarId == calendarId, ct);
}
