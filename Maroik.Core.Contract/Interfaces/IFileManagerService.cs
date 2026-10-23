using Maroik.Core.Contract.Dtos;

namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Service interface for the File Manager: one drive per user of logical folders and GUID-stored files. Every operation is scoped to
/// the given account; another account's item is answered as "not found" (S5). Every structural write first locks the account row, so
/// a user's concurrent writes (uploads, moves, deletes) are applied one at a time and can never exceed the quota (Q4).
/// </summary>
public interface IFileManagerService
{
    /// <summary>The folder tree and the storage usage.</summary>
    Task<FileManagerDriveResponse> GetDriveAsync(string accountEmail, CancellationToken ct = default);

    /// <summary>The breadcrumb and items of <paramref name="folderId"/> (null = the root); "not found" for a folder the account does not own.</summary>
    Task<(ServiceResult Result, FileManagerFolderResponse? Folder)> GetFolderAsync(string accountEmail, long? folderId, CancellationToken ct = default);

    /// <summary>The finished items whose name contains <paramref name="query"/> (ignoring case), each with its location (U5).</summary>
    Task<List<FileManagerItemResponse>> SearchAsync(string accountEmail, string? query, CancellationToken ct = default);

    /// <summary>
    /// Creates a folder named <paramref name="name"/> in <paramref name="parentId"/>. With <paramref name="merge"/> (a folder upload, F2-C)
    /// an existing folder of that name is reused; without it the name must be free. Returns the folder's id.
    /// </summary>
    Task<(ServiceResult Result, long? FolderId)> CreateFolderAsync(string accountEmail, long? parentId, string? name, bool merge, CancellationToken ct = default);

    /// <summary>Renames an item; refused when the name is taken in its folder (F4).</summary>
    Task<ServiceResult> RenameAsync(string accountEmail, long id, string? name, CancellationToken ct = default);

    /// <summary>Moves items into <paramref name="targetFolderId"/> (null = the root), all or none; refused on a name conflict or a cycle (F5).</summary>
    Task<ServiceResult> MoveAsync(string accountEmail, IReadOnlyCollection<long> ids, long? targetFolderId, CancellationToken ct = default);

    /// <summary>Deletes items, a folder with everything under it, and then their stored files (F6, P4).</summary>
    Task<ServiceResult> DeleteAsync(string accountEmail, IReadOnlyCollection<long> ids, CancellationToken ct = default);

    /// <summary>Whether uploads totalling <paramref name="totalBytes"/> fit the remaining quota (the check before a batch starts, F2).</summary>
    Task<ServiceResult> CheckQuotaAsync(string accountEmail, long totalBytes, CancellationToken ct = default);

    /// <summary>Starts the upload of one file: validates its name and size, resolves a name conflict, checks and reserves the quota.</summary>
    Task<(ServiceResult Result, FileManagerUploadStart? Start)> BeginUploadAsync(string accountEmail, FileManagerUploadRequest request, CancellationToken ct = default);

    /// <summary>Sends the next chunk of an upload to file storage; returns the length received so far.</summary>
    Task<(ServiceResult Result, long Length)> AppendChunkAsync(string accountEmail, Guid uploadId, long offset, Stream chunk, long chunkLength, CancellationToken ct = default);

    /// <summary>
    /// Completes an upload: file storage scans and stores it, then the file becomes a normal item. With <paramref name="overwrite"/> a
    /// finished file of the same name is replaced (deleted); without it such a file makes the upload fail.
    /// </summary>
    Task<ServiceResult> CompleteUploadAsync(string accountEmail, Guid uploadId, bool overwrite, CancellationToken ct = default);

    /// <summary>Abandons an unfinished upload.</summary>
    Task<ServiceResult> AbortUploadAsync(string accountEmail, Guid uploadId, CancellationToken ct = default);

    /// <summary>Opens a file for download (streamed from file storage, F3).</summary>
    Task<(ServiceResult Result, AttachmentDownload? File)> OpenFileAsync(string accountEmail, long id, CancellationToken ct = default);

    /// <summary>The bytes and proven Content-Type of a previewable image (F9).</summary>
    Task<(ServiceResult Result, FileManagerPreview? Preview)> OpenPreviewAsync(string accountEmail, long id, CancellationToken ct = default);

    /// <summary>The files a multi-download fetches (F8): each selected file, and every file at any depth under each selected folder.</summary>
    Task<(ServiceResult Result, List<FileManagerItemResponse> Files)> ListFilesForDownloadAsync(string accountEmail, IReadOnlyCollection<long> ids, CancellationToken ct = default);
}
