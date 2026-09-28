using Maroik.Core.Domain.Board;
namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Repository abstraction over <see cref="BoardAttachedFile"/> persistence.
/// </summary>
public interface IBoardAttachedFileRepository : IGenericRepository<BoardAttachedFile>
{
    /// <summary>
    /// Batch-loads attached file metadata for multiple board IDs.
    /// Returns a dictionary keyed by board ID; value is null when a post has no attachment.
    /// </summary>
    Task<Dictionary<long, BoardAttachedFile?>> GetByBoardIdsAsync(IEnumerable<long> boardIds, CancellationToken ct = default);

    /// <summary>Returns the attached file for the given board post, or null if none exists.</summary>
    Task<BoardAttachedFile?> FindByBoardIdAsync(long boardId, CancellationToken ct = default);
}
