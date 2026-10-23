using Maroik.Core.Domain.FileManagers;
namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Repository abstraction over <see cref="FileManagerNode"/> persistence (the folders and files of a user's File Manager drive). Every
/// lookup and delete is scoped to the owning account's email, so another user's node id behaves as "not found" (S5).
/// </summary>
public interface IFileManagerNodeRepository : IGenericRepository<FileManagerNode>
{
    /// <summary>Returns every node of the account's drive (folders, files and unfinished uploads), unordered.</summary>
    Task<List<FileManagerNode>> GetByAccountEmailAsync(string accountEmail, CancellationToken ct = default);

    /// <summary>Returns the node with the given ID when it belongs to the account, or null otherwise.</summary>
    Task<FileManagerNode?> FindByEmailAndIdAsync(string accountEmail, long id, CancellationToken ct = default);

    /// <summary>Returns the file stored at <paramref name="storagePath"/> when it belongs to the account, or null otherwise.</summary>
    Task<FileManagerNode?> FindByEmailAndStoragePathAsync(string accountEmail, string storagePath, CancellationToken ct = default);

    /// <summary>Deletes the account's nodes with the given IDs (another account's IDs are left alone).</summary>
    Task DeleteByEmailAndIdsAsync(string accountEmail, IReadOnlyCollection<long> ids, CancellationToken ct = default);
}
