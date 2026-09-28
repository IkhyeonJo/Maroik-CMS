using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Calendar;
using Maroik.Core.PostgreSQL.Data;
using Microsoft.EntityFrameworkCore;
using OrmFile = Maroik.Core.PostgreSQL.Models.CalendarEventAttachedFile;

namespace Maroik.Core.Repository.Repositories;

/// <summary>
/// EF Core repository for <see cref="CalendarEventAttachedFile"/> domain objects.
/// </summary>
public class CalendarEventAttachedFileRepository(ApplicationDbContext context)
    : GenericRepository<CalendarEventAttachedFile, OrmFile>(context), ICalendarEventAttachedFileRepository
{
    /// <summary>The underlying <see cref="DbSet{TEntity}"/> for <see cref="OrmFile"/> rows.</summary>
    protected override DbSet<OrmFile> Set => Context.CalendarEventAttachedFiles;

    /// <summary>Maps a persisted <see cref="OrmFile"/> row to the <see cref="CalendarEventAttachedFile"/> domain object.</summary>
    protected override CalendarEventAttachedFile ToDomain(OrmFile e)
        => CalendarEventAttachedFile.Reconstitute(e.Id, e.CalendarEventId, e.Size, e.Name, e.Extension, e.Path);

    /// <summary>Maps a <see cref="CalendarEventAttachedFile"/> domain object to its <see cref="OrmFile"/> persistence representation.</summary>
    protected override OrmFile ToEntity(CalendarEventAttachedFile f) => new()
    {
        Id = f.Id,
        CalendarEventId = f.CalendarEventId,
        Size = f.Size,
        Name = f.Name,
        Extension = f.Extension,
        Path = f.Path
    };

    /// <inheritdoc />
    public Task<CalendarEventAttachedFile?> FindByCalendarEventIdAsync(long calendarEventId, CancellationToken ct = default)
        => QueryFirstAsync(e => e.CalendarEventId == calendarEventId, ct: ct);

    /// <inheritdoc />
    public Task DeleteByCalendarEventIdAsync(long calendarEventId, CancellationToken ct = default)
        => DeleteWhereAsync(e => e.CalendarEventId == calendarEventId, ct);
}
