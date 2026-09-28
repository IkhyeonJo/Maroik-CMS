using Maroik.Core.Domain.Board;
namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Repository abstraction over <see cref="BoardComment"/> persistence.
/// </summary>
public interface IBoardCommentRepository : IGenericRepository<BoardComment>
{
    /// <summary>Batch-counts comments for multiple board IDs. Returns a dictionary keyed by board ID.</summary>
    Task<Dictionary<long, int>> GetCommentCountsByBoardIdsAsync(IEnumerable<long> boardIds, CancellationToken ct = default);

    /// <summary>Returns all non-deleted comments for the given board post in display order.</summary>
    Task<List<BoardComment>> GetByBoardIdOrderedAsync(long boardId, CancellationToken ct = default);

    /// <summary>Returns the non-deleted comment with the given ID, or null if not found or already deleted.</summary>
    Task<BoardComment?> FindByIdAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Returns the non-deleted comment with the given ID, row-locked (<c>FOR UPDATE</c>) for the
    /// rest of the ambient transaction, or null if not found or already deleted.
    /// </summary>
    Task<BoardComment?> FindByIdForUpdateAsync(long id, CancellationToken ct = default);
}
