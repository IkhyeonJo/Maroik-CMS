using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.FileManagers;
using Maroik.Core.PostgreSQL.Contexts;
using Microsoft.EntityFrameworkCore;
using OrmFileManagerNode = Maroik.Core.PostgreSQL.Models.FileManagerNode;

namespace Maroik.Core.Repository.Repositories;

/// <summary>
/// EF Core repository for <see cref="FileManagerNode"/> domain objects (the folders and files of a File Manager drive, one table).
/// Every query and delete is filtered by the owning account's email as well as the id or path, so another account's node is neither
/// found nor deleted (S5). Deleting a folder row also deletes everything under it (<c>FileManagerNode_fk_1</c> ON DELETE CASCADE).
/// </summary>
public class FileManagerNodeRepository(ApplicationDbContext context)
    : GenericRepository<FileManagerNode, OrmFileManagerNode>(context), IFileManagerNodeRepository
{
    /// <summary>The underlying <see cref="DbSet{TEntity}"/> for <see cref="OrmFileManagerNode"/> rows.</summary>
    protected override DbSet<OrmFileManagerNode> Set => Context.FileManagerNodes;

    /// <summary>Maps a persisted <see cref="OrmFileManagerNode"/> row to the <see cref="FileManagerNode"/> domain object.</summary>
    protected override FileManagerNode ToDomain(OrmFileManagerNode e) => FileManagerNode.Reconstitute(
        e.Id, e.AccountEmail, e.ParentId, e.Name, e.IsFolder, e.Size, e.StoragePath, e.Uploading, e.Created, e.Updated);

    /// <summary>Maps a <see cref="FileManagerNode"/> domain object to its <see cref="OrmFileManagerNode"/> persistence representation.</summary>
    protected override OrmFileManagerNode ToEntity(FileManagerNode node) => new()
    {
        Id = node.Id,
        AccountEmail = node.AccountEmail.Value,
        ParentId = node.ParentId,
        Name = node.Name,
        IsFolder = node.IsFolder,
        Size = node.Size,
        StoragePath = node.StoragePath,
        Uploading = node.Uploading,
        Created = node.Created,
        Updated = node.Updated
    };

    /// <inheritdoc />
    public Task<List<FileManagerNode>> GetByAccountEmailAsync(string accountEmail, CancellationToken ct = default)
        => QueryAsync(e => e.AccountEmail == accountEmail, noTracking: true, ct: ct);

    /// <inheritdoc />
    public Task<FileManagerNode?> FindByEmailAndIdAsync(string accountEmail, long id, CancellationToken ct = default)
        => QueryFirstAsync(e => e.AccountEmail == accountEmail && e.Id == id, ct: ct);

    /// <inheritdoc />
    public Task<FileManagerNode?> FindByEmailAndStoragePathAsync(string accountEmail, string storagePath, CancellationToken ct = default)
        => QueryFirstAsync(e => e.AccountEmail == accountEmail && e.StoragePath == storagePath, ct: ct);

    /// <inheritdoc />
    public Task DeleteByEmailAndIdsAsync(string accountEmail, IReadOnlyCollection<long> ids, CancellationToken ct = default)
        => DeleteWhereAsync(e => e.AccountEmail == accountEmail && ids.Contains(e.Id), ct);
}
