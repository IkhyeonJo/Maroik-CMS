using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Board;
using Maroik.Core.PostgreSQL.Data;
using Microsoft.EntityFrameworkCore;
using OrmBoardComment = Maroik.Core.PostgreSQL.Models.BoardComment;

namespace Maroik.Core.Repository.Repositories;

/// <summary>
/// EF Core repository for <see cref="BoardComment"/> domain objects.
/// Includes a batched comment-count query used to populate board list views efficiently.
/// </summary>
public class BoardCommentRepository(ApplicationDbContext context)
    : GenericRepository<BoardComment, OrmBoardComment>(context), IBoardCommentRepository
{
    /// <summary>The underlying <see cref="DbSet{TEntity}"/> for <see cref="OrmBoardComment"/> rows.</summary>
    protected override DbSet<OrmBoardComment> Set => Context.BoardComments;

    /// <inheritdoc />
    public async Task<Dictionary<long, int>> GetCommentCountsByBoardIdsAsync(IEnumerable<long> boardIds, CancellationToken ct = default)
    {
        List<long> ids = [.. boardIds];

        Dictionary<long, int> counts = await Context.BoardComments
            .Where(x => ids.Contains(x.BoardId) && !x.Deleted)
            .GroupBy(x => x.BoardId)
            .Select(g => new { BoardId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BoardId, x => x.Count, ct);

        foreach (long id in ids.Where(id => !counts.ContainsKey(id)))
            counts[id] = 0;

        return counts;
    }

    /// <summary>Maps a persisted <see cref="OrmBoardComment"/> row to the <see cref="BoardComment"/> domain object.</summary>
    protected override BoardComment ToDomain(OrmBoardComment e) => BoardComment.Reconstitute(
        e.Id, e.BoardId, e.Order, e.AvatarImagePath, e.Writer,
        e.Content, e.Created, e.Deleted);

    /// <summary>Maps a <see cref="BoardComment"/> domain object to its <see cref="OrmBoardComment"/> persistence representation.</summary>
    protected override OrmBoardComment ToEntity(BoardComment c) => new()
    {
        Id = c.Id,
        BoardId = c.BoardId,
        Order = c.Order,
        AvatarImagePath = c.AvatarImagePath ?? "",
        Writer = c.Writer,
        Content = c.Content ?? "",
        Created = c.Created,
        Deleted = c.Deleted
    };

    /// <inheritdoc />
    public Task<List<BoardComment>> GetByBoardIdOrderedAsync(long boardId, CancellationToken ct = default)
        => QueryAsync(e => e.BoardId == boardId && !e.Deleted, q => q.OrderBy(e => e.Order), ct: ct);

    /// <inheritdoc />
    public Task<BoardComment?> FindByIdAsync(long id, CancellationToken ct = default)
        => QueryFirstAsync(e => e.Id == id && !e.Deleted, ct: ct);

    /// <inheritdoc />
    public async Task<BoardComment?> FindByIdForUpdateAsync(long id, CancellationToken ct = default)
    {
        // Raw SQL is required because EF Core LINQ cannot emit FOR UPDATE. AsNoTracking so a
        // pre-lock instance already tracked in this context can't snap back and defeat the lock
        // (see BoardRepository.FindActiveByIdForUpdateAsync for the same rationale).
        var entities = await Set.FromSqlInterpolated(
            $"""
             SELECT *
             FROM "BoardComment"
             WHERE "Id" = {id} AND "Deleted" = false
             LIMIT 1
             FOR UPDATE
             """).AsNoTracking().ToListAsync(ct);

        var entity = entities.FirstOrDefault();
        return entity == null ? null : ToDomain(entity);
    }
}
