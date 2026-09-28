using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Board;
namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Repository abstraction over <see cref="Board"/> (forum / private-note post) persistence.
/// </summary>
public interface IBoardRepository : IGenericRepository<Board>
{
    /// <summary>Increments the view counter of the board post by 1.</summary>
    Task IncrementViewAsync(long id, CancellationToken ct = default);

    /// <summary>Inserts a new board post and returns its generated ID.</summary>
    Task<long> WriteBoardAsync(Board board, CancellationToken ct = default);

    /// <summary>Returns all non-deleted posts of the specified board type.</summary>
    Task<List<Board>> GetByTypeAsync(string type, CancellationToken ct = default);

    /// <summary>Returns all non-deleted, pinned ("Noticed") posts of the specified board type, newest first.</summary>
    Task<List<Board>> GetNoticedAsync(string type, CancellationToken ct = default);

    /// <summary>
    /// Returns one page of non-deleted, non-noticed posts matching <paramref name="query"/>,
    /// executed as a single SQL query (filter, search, visibility rules, ordering, and paging all
    /// pushed down — the whole board-type bucket is never materialized into memory).
    /// </summary>
    Task<(List<Board> Items, int TotalCount)> QueryPageAsync(BoardPageQuery query, CancellationToken ct = default);

    /// <summary>Returns the non-deleted post with the given ID, or null if not found.</summary>
    Task<Board?> FindActiveByIdAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Returns the non-deleted post with the given ID, row-locked (<c>SELECT ... FOR UPDATE</c>)
    /// for the duration of the current transaction. Callers must hold an open
    /// <see cref="IUnitOfWork"/> transaction. Used to serialize concurrent writers against the
    /// same post (e.g. comment ordering) instead of allowing a read-then-write race.
    /// </summary>
    Task<Board?> FindActiveByIdForUpdateAsync(long id, CancellationToken ct = default);
}
