using ErrorOr;
using Maroik.Core.Domain.Errors;
using Maroik.Core.Domain.Primitives;
using Maroik.Core.Domain.ValueObjects;

namespace Maroik.Core.Domain.FileManagers;

/// <summary>
/// Aggregate root for one folder or file of a user's File Manager drive. Folders are logical — they exist only in the database (P3);
/// the tree is built from <see cref="ParentId"/> (null = the drive root, "My Drive"). A file is stored in file storage under a GUID
/// name in <see cref="FileManagerUploadPolicy.StorageDirectory"/> (P1); the name the user typed lives only here (P2). A file is
/// <see cref="Uploading"/> from the moment its upload starts — its declared size already counts against the quota (Q4) — until the
/// upload is scanned and completed.
/// </summary>
public sealed class FileManagerNode : AggregateRoot<long>
{
    /// <summary>Email of the owning account.</summary>
    public Email AccountEmail { get; }

    /// <summary>The folder this node is in, or null for the drive root.</summary>
    public long? ParentId { get; private set; }

    /// <summary>The name shown to the user (NFKC, validated by <see cref="FileManagerNamePolicy"/>), unique within its folder.</summary>
    public string Name { get; private set; }

    /// <summary>True for a folder, false for a file.</summary>
    public bool IsFolder { get; }

    /// <summary>The file's size in bytes (its declared size while uploading); 0 for a folder.</summary>
    public long Size { get; }

    /// <summary>The file's storage path (<c>upload/FileManager/{GUID}</c>); null for a folder.</summary>
    public string? StoragePath { get; }

    /// <summary>True while the file's upload has not completed.</summary>
    public bool Uploading { get; private set; }

    /// <summary>UTC timestamp when the node was created (for an upload: when it started).</summary>
    public DateTime Created { get; }

    /// <summary>UTC timestamp of the most recent change.</summary>
    public DateTime Updated { get; private set; }

    /// <summary>The GUID name the file is stored under (also the id of its upload), or null for a folder.</summary>
    public Guid? StorageId =>
        StoragePath != null && Guid.TryParseExact(Path.GetFileName(StoragePath), "D", out Guid id) ? id : null;

    /// <summary>Assigns every field verbatim (used by the factories and reconstitution).</summary>
    private FileManagerNode(long id, Email accountEmail, long? parentId, string name, bool isFolder, long size, string? storagePath,
        bool uploading, DateTime created, DateTime updated) : base(id)
    {
        AccountEmail = accountEmail;
        ParentId = parentId;
        Name = name;
        IsFolder = isFolder;
        Size = size;
        StoragePath = storagePath;
        Uploading = uploading;
        Created = created;
        Updated = updated;
    }

    /// <summary>
    /// Rebuilds a <see cref="FileManagerNode"/> from trusted raw values from the repository layer.
    /// Bypasses validation — use only with data from the trusted repository layer.
    /// </summary>
    public static FileManagerNode Reconstitute(long id, string accountEmail, long? parentId, string name, bool isFolder, long size,
        string? storagePath, bool uploading, DateTime created, DateTime updated) =>
        new(id, Email.FromTrustedSource(accountEmail), parentId, name, isFolder, size, storagePath, uploading, created, updated);

    /// <summary>Creates a folder named <paramref name="name"/> in <paramref name="parentId"/> (null = the root).</summary>
    public static ErrorOr<FileManagerNode> CreateFolder(string? accountEmail, long? parentId, string? name, DateTime utcNow)
    {
        var nameResult = FileManagerNamePolicy.Validate(name);
        if (nameResult.IsError) return nameResult.Errors;

        var emailResult = Email.Create(accountEmail);
        if (emailResult.IsError) return emailResult.Errors;

        return new FileManagerNode(0, emailResult.Value, parentId, nameResult.Value, true, 0, null, false, utcNow, utcNow);
    }

    /// <summary>
    /// Starts the upload of a file of <paramref name="size"/> bytes named <paramref name="name"/> into <paramref name="parentId"/>,
    /// to be stored as <paramref name="storageId"/>. The file is <see cref="Uploading"/> until <see cref="CompleteUpload"/>.
    /// </summary>
    public static ErrorOr<FileManagerNode> StartUpload(string? accountEmail, long? parentId, string? name, long size, Guid storageId, DateTime utcNow)
    {
        var nameResult = FileManagerNamePolicy.Validate(name);
        if (nameResult.IsError) return nameResult.Errors;

        var sizeResult = FileManagerUploadPolicy.CheckSize(size);
        if (sizeResult.IsError) return sizeResult.Errors;

        var emailResult = Email.Create(accountEmail);
        if (emailResult.IsError) return emailResult.Errors;

        string storagePath = $"{FileManagerUploadPolicy.StorageDirectory}/{storageId:D}";
        return new FileManagerNode(0, emailResult.Value, parentId, nameResult.Value, false, size, storagePath, true, utcNow, utcNow);
    }

    /// <summary>Ends the upload: the file is scanned and stored, and is now a normal file of the drive.</summary>
    public void CompleteUpload(DateTime utcNow)
    {
        Uploading = false;
        Updated = utcNow;
    }

    /// <summary>Whether this is an upload left unfinished for longer than <see cref="FileManagerUploadPolicy.StaleUploadAge"/> (Q5).</summary>
    public bool IsStaleUpload(DateTime utcNow) => Uploading && FileManagerUploadPolicy.IsStale(Created, utcNow);

    /// <summary>Renames the node. Whether the name is free in its folder is the caller's check (and the database's).</summary>
    public ErrorOr<Success> Rename(string? name, DateTime utcNow)
    {
        var nameResult = FileManagerNamePolicy.Validate(name);
        if (nameResult.IsError) return nameResult.Errors;

        Name = nameResult.Value;
        Updated = utcNow;
        return Result.Success;
    }

    /// <summary>
    /// Moves the node into <paramref name="targetFolderId"/> (null = the root). <paramref name="targetAncestry"/> is the target folder
    /// and every folder above it (<see cref="FileManagerTree.Ancestry"/>): a folder found in it would be moved into itself or into one
    /// of its own sub-folders, a cycle, which is refused (S5).
    /// </summary>
    public ErrorOr<Success> MoveTo(long? targetFolderId, IReadOnlyCollection<long> targetAncestry, DateTime utcNow)
    {
        if (IsFolder && targetAncestry.Contains(Id))
            return DomainError.Validation("FileManager.MoveIntoItself", "A folder cannot be moved into itself or one of its sub-folders.");

        ParentId = targetFolderId;
        Updated = utcNow;
        return Result.Success;
    }
}
