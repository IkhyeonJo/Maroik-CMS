using Maroik.Core.Contract.Enums;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Messages;
using Maroik.FileStorage.Helpers;
using Maroik.FileStorage.Interfaces;
using Maroik.FileStorage.Settings;
using Microsoft.Extensions.Options;

namespace Maroik.FileStorage.Services;

/// <summary>
/// The File Manager's large-file flow on disk. Nothing is held in memory: chunks are appended to
/// <c>upload/FileManager/.staging/{uploadId}</c>, the completed file is scanned by streaming it from disk, and only a clean file of the
/// declared size is moved to <c>upload/FileManager/{uploadId}</c>. The upload id is the GUID the Website stores the file under; anything
/// that is not a GUID is refused before the disk is touched, so an id can never name a path. Every path goes through
/// <see cref="StoragePath"/> and <see cref="StorageFiles"/> like the rest of the service.
/// </summary>
public class FileManagerStorageService(IClamavClient clamavClient, IOptions<FileStorageSetting> settings) : IFileManagerStorageService
{
    /// <summary>
    /// The time limit of one File Manager scan. Files reach 2 GB, far beyond an attachment, so the client's 30-second default would
    /// refuse them; clamd's own MaxScanTime (set in the compose files) ends a scan that runs longer as "could not be scanned".
    /// </summary>
    public static readonly TimeSpan ScanTimeout = TimeSpan.FromMinutes(15);

    /// <summary>The directory the stored File Manager files live in (P1).</summary>
    private const string FileManagerDirectory = "upload/FileManager";

    /// <summary>The directory the unfinished uploads' staging files live in.</summary>
    private const string StagingDirectory = "upload/FileManager/.staging";

    /// <summary>The upload id in its canonical form, or null when it is not a GUID.</summary>
    private static string? CanonicalId(string? uploadId) =>
        Guid.TryParseExact(uploadId, "D", out Guid id) ? id.ToString("D") : null;

    /// <summary><paramref name="relativeDirectory"/> confined to the storage root.</summary>
    private StoragePath? Directory(string relativeDirectory) =>
        StoragePath.ResolveWithinRoot(settings.Value.StorageRootPath, relativeDirectory);

    /// <summary>The staging file of <paramref name="uploadId"/>, or null for an id that is not a GUID.</summary>
    private StoragePath? StagingFile(string? uploadId) =>
        CanonicalId(uploadId) is { } id ? Directory(StagingDirectory)?.InDirectory(id) : null;

    /// <summary>The stored file of <paramref name="uploadId"/>, or null for an id that is not a GUID.</summary>
    private StoragePath? StoredFile(string? uploadId) =>
        CanonicalId(uploadId) is { } id ? Directory(FileManagerDirectory)?.InDirectory(id) : null;

    /// <inheritdoc />
    public async Task<(string? Error, long Length)> AppendChunkAsync(string? uploadId, long offset, Stream chunk, CancellationToken ct = default)
    {
        StoragePath? staging = StagingFile(uploadId);
        StoragePath? stagingDirectory = Directory(StagingDirectory);
        if (staging == null || stagingDirectory == null)
            return (FileStorageRefusals.InvalidUpload, 0);

        StorageFiles.CreateDirectory(stagingDirectory);
        long received = StorageFiles.Exists(staging) ? StorageFiles.Length(staging) : 0;
        // Chunks arrive one at a time in order; a resent or skipped chunk would corrupt the file, so only the next one is taken.
        if (offset != received)
            return (FileStorageRefusals.OffsetMismatch, received);

        await using (Stream file = StorageFiles.OpenAppend(staging))
        {
            await chunk.CopyToAsync(file, ct);
            await file.FlushAsync(ct);
        }

        return (null, StorageFiles.Length(staging));
    }

    /// <inheritdoc />
    public async Task<string?> CompleteAsync(string? uploadId, long expectedSize, CancellationToken ct = default)
    {
        StoragePath? staging = StagingFile(uploadId);
        StoragePath? stored = StoredFile(uploadId);
        StoragePath? stagingDirectory = Directory(StagingDirectory);
        if (staging == null || stored == null || stagingDirectory == null)
            return FileStorageRefusals.InvalidUpload;

        // An empty file sends no chunk: its staging file is created here.
        if (expectedSize == 0 && !StorageFiles.Exists(staging))
        {
            StorageFiles.CreateDirectory(stagingDirectory);
            await using Stream empty = StorageFiles.OpenAppend(staging);
        }

        if (!StorageFiles.Exists(staging))
            return FileStorageRefusals.SizeMismatch;

        if (StorageFiles.Length(staging) != expectedSize)
        {
            StorageFiles.Delete(staging);
            return FileStorageRefusals.SizeMismatch;
        }

        // Every upload gets a fresh GUID, so a stored file under it means something is wrong upstream: never overwrite it.
        if (StorageFiles.Exists(stored))
        {
            StorageFiles.Delete(staging);
            return FileStorageRefusals.InvalidUpload;
        }

        ClamavScanResult scan;
        await using (Stream file = StorageFiles.OpenRead(staging))
            scan = await clamavClient.ScanWithClamavAsync(file, settings.Value.ClamavHost ?? "", settings.Value.ClamavPort, ScanTimeout, ct);

        // Only Clean is stored (S2). Infected and "could not be scanned" are both refusals, reported apart.
        if (scan != ClamavScanResult.Clean)
        {
            StorageFiles.Delete(staging);
            return scan == ClamavScanResult.Infected ? FileStorageRefusals.Infected : FileStorageRefusals.ScanUnavailable;
        }

        StorageFiles.Move(staging, stored);
        StorageFiles.MakeReadable(stored);
        return null;
    }

    /// <inheritdoc />
    public bool Abort(string? uploadId)
    {
        StoragePath? staging = StagingFile(uploadId);
        if (staging == null)
            return false;

        StorageFiles.Delete(staging);
        return true;
    }

    /// <inheritdoc />
    public string? Delete(string? filePath)
    {
        // Only "upload/FileManager/{GUID}" itself: an attachment, a staging file or a traversal is not a File Manager file.
        string prefix = FileManagerDirectory + "/";
        string? id = filePath != null && filePath.StartsWith(prefix, StringComparison.Ordinal) ? CanonicalId(filePath[prefix.Length..]) : null;
        StoragePath? stored = id != null && filePath == prefix + id ? StoredFile(id) : null;
        if (stored == null)
            return FileStorageRefusals.NotAFileManagerFile;

        if (!StorageFiles.Exists(stored))
            return FileStorageRefusals.FileNotFound;

        StorageFiles.Delete(stored);
        return null;
    }
}
