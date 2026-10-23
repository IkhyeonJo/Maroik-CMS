using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.FileManagers;

namespace Maroik.Core.Service.Tests.Fixtures;

/// <summary>
/// An in-memory <see cref="IFileManagerNodeRepository"/> for the File Manager service tests. Like the database it assigns ids, hands out
/// copies (so a change the service makes reaches storage only through <see cref="UpdateEntityAsync"/>), scopes every lookup to the
/// owning account, and deletes everything under a deleted folder (FileManagerNode_fk_1 ON DELETE CASCADE).
/// </summary>
internal sealed class InMemoryFileManagerNodeRepository : IFileManagerNodeRepository
{
    /// <summary>The stored nodes.</summary>
    private readonly List<FileManagerNode> _nodes = [];

    /// <summary>The next id to assign.</summary>
    private long _nextId = 1;

    /// <summary>Every stored node (copies).</summary>
    public IReadOnlyList<FileManagerNode> All => [.. _nodes.Select(Copy)];

    /// <summary>The stored node with <paramref name="id"/>, or null.</summary>
    public FileManagerNode? Get(long id) => _nodes.Where(n => n.Id == id).Select(Copy).SingleOrDefault();

    /// <summary>A detached copy of <paramref name="n"/>, with <paramref name="id"/> when given.</summary>
    private static FileManagerNode CopyWithId(FileManagerNode n, long id) =>
        FileManagerNode.Reconstitute(id, n.AccountEmail.Value, n.ParentId, n.Name, n.IsFolder, n.Size, n.StoragePath, n.Uploading, n.Created, n.Updated);

    /// <summary>A detached copy of <paramref name="n"/>.</summary>
    private static FileManagerNode Copy(FileManagerNode n) => CopyWithId(n, n.Id);

    /// <inheritdoc />
    public Task CreateAsync(FileManagerNode domain, CancellationToken ct = default)
    {
        _nodes.Add(CopyWithId(domain, _nextId++));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateEntityAsync(FileManagerNode domain, CancellationToken ct = default)
    {
        int index = _nodes.FindIndex(n => n.Id == domain.Id);
        if (index < 0) throw new InvalidOperationException($"No node {domain.Id}");
        _nodes[index] = Copy(domain);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<List<FileManagerNode>> GetByAccountEmailAsync(string accountEmail, CancellationToken ct = default) =>
        Task.FromResult<List<FileManagerNode>>([.. _nodes.Where(n => n.AccountEmail.Value == accountEmail).Select(Copy)]);

    /// <inheritdoc />
    public Task<FileManagerNode?> FindByEmailAndIdAsync(string accountEmail, long id, CancellationToken ct = default) =>
        Task.FromResult(_nodes.Where(n => n.AccountEmail.Value == accountEmail && n.Id == id).Select(Copy).SingleOrDefault());

    /// <inheritdoc />
    public Task<FileManagerNode?> FindByEmailAndStoragePathAsync(string accountEmail, string storagePath, CancellationToken ct = default) =>
        Task.FromResult(_nodes.Where(n => n.AccountEmail.Value == accountEmail && n.StoragePath == storagePath).Select(Copy).SingleOrDefault());

    /// <inheritdoc />
    public Task DeleteByEmailAndIdsAsync(string accountEmail, IReadOnlyCollection<long> ids, CancellationToken ct = default)
    {
        HashSet<long> doomed = [.. _nodes.Where(n => n.AccountEmail.Value == accountEmail && ids.Contains(n.Id)).Select(n => n.Id)];
        // The cascade: keep adding the children of anything deleted.
        while (_nodes.Where(n => n.ParentId is { } p && doomed.Contains(p) && !doomed.Contains(n.Id)).Select(n => n.Id).ToList() is { Count: > 0 } more)
            doomed.UnionWith(more);
        _nodes.RemoveAll(n => doomed.Contains(n.Id));
        return Task.CompletedTask;
    }
}
