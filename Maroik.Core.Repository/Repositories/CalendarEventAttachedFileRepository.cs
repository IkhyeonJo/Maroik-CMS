using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Calendar;
using Maroik.Core.PostgreSQL.Data;
using Microsoft.EntityFrameworkCore;
using OrmCalendarEventAttachedFile = Maroik.Core.PostgreSQL.Models.CalendarEventAttachedFile;

namespace Maroik.Core.Repository.Repositories;

/// <summary>
/// EF Core repository for <see cref="CalendarEventAttachedFile"/> domain objects.
/// </summary>
public class CalendarEventAttachedFileRepository(ApplicationDbContext context)
    : GenericRepository<CalendarEventAttachedFile, OrmCalendarEventAttachedFile>(context), ICalendarEventAttachedFileRepository
{
    /// <summary>The underlying <see cref="DbSet{TEntity}"/> for <see cref="OrmCalendarEventAttachedFile"/> rows.</summary>
    protected override DbSet<OrmCalendarEventAttachedFile> Set => Context.CalendarEventAttachedFiles;

    /// <summary>Maps a persisted <see cref="OrmCalendarEventAttachedFile"/> row to the <see cref="CalendarEventAttachedFile"/> domain object.</summary>
    protected override CalendarEventAttachedFile ToDomain(OrmCalendarEventAttachedFile e)
        => CalendarEventAttachedFile.Reconstitute(e.Id, e.CalendarEventId, e.Size, e.Name, e.Extension, e.Path);

    /// <summary>Maps a <see cref="CalendarEventAttachedFile"/> domain object to its <see cref="OrmCalendarEventAttachedFile"/> persistence representation.</summary>
    protected override OrmCalendarEventAttachedFile ToEntity(CalendarEventAttachedFile f) => new()
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
}
