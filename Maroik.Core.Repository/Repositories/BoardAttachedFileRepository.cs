using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Board;
using Maroik.Core.PostgreSQL.Data;
using Microsoft.EntityFrameworkCore;
using OrmFile = Maroik.Core.PostgreSQL.Models.BoardAttachedFile;

namespace Maroik.Core.Repository.Repositories;

/// <summary>
/// EF Core repository for <see cref="BoardAttachedFile"/> domain objects.
/// Provides a batched lookup that retrieves one representative file per board ID.
/// </summary>
public class BoardAttachedFileRepository(ApplicationDbContext context)
    : GenericRepository<BoardAttachedFile, OrmFile>(context), IBoardAttachedFileRepository
{
    /// <summary>The underlying <see cref="DbSet{TEntity}"/> for <see cref="OrmFile"/> rows.</summary>
    protected override DbSet<OrmFile> Set => Context.BoardAttachedFiles;

    /// <inheritdoc />
    public async Task<Dictionary<long, BoardAttachedFile?>> GetByBoardIdsAsync(IEnumerable<long> boardIds, CancellationToken ct = default)
    {
        List<long> ids = [.. boardIds];
        // AsNoTracking: this is a pure read-only display projection, not an entity that will be
        // updated through this context. ToLookup once instead of a FirstOrDefault scan per id,
        // so this is O(files + ids) rather than O(files * ids) for a large board list.
        List<OrmFile> files = await Context.BoardAttachedFiles
            .AsNoTracking()
            .Where(x => ids.Contains(x.BoardId))
            .ToListAsync(ct);

        ILookup<long, OrmFile> filesByBoardId = files.ToLookup(f => f.BoardId);
        return ids.ToDictionary(
            id => id,
            id => filesByBoardId[id].FirstOrDefault() is { } f ? ToDomain(f) : null);
    }

    /// <summary>Maps a persisted <see cref="OrmFile"/> row to the <see cref="BoardAttachedFile"/> domain object.</summary>
    protected override BoardAttachedFile ToDomain(OrmFile e)
        => BoardAttachedFile.Reconstitute(e.Id, e.BoardId, e.Size, e.Name, e.Extension, e.Path);

    /// <summary>Maps a <see cref="BoardAttachedFile"/> domain object to its <see cref="OrmFile"/> persistence representation.</summary>
    protected override OrmFile ToEntity(BoardAttachedFile f) => new()
    {
        Id = f.Id,
        BoardId = f.BoardId,
        Size = f.Size,
        Name = f.Name,
        Extension = f.Extension,
        Path = f.Path
    };

    /// <inheritdoc />
    public Task<BoardAttachedFile?> FindByBoardIdAsync(long boardId, CancellationToken ct = default)
        => QueryFirstAsync(e => e.BoardId == boardId, ct: ct);
}
