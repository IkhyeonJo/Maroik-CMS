using System.Collections.Concurrent;
using Maroik.Core.Contract.Enums;
using Maroik.Core.Contract.Interfaces;

namespace Maroik.Website.Tests.Fixtures;

/// <summary>
/// In-memory stand-in for the file-storage service client: uploads are kept in a dictionary (and can be
/// pre-seeded), downloads return what is stored, and the outcome of <see cref="UploadWithResultAsync"/> /
/// <see cref="UploadAsync"/> can be forced so the virus-scan and storage-failure branches are reachable.
/// </summary>
public sealed class FakeFileClient : IFileClient
{
    /// <summary>Stored files keyed by path (without a leading slash).</summary>
    private readonly ConcurrentDictionary<string, (byte[] Bytes, string ContentType)> _files = new();

    /// <summary>What every upload reports; defaults to <see cref="FileUploadResult.Stored"/>.</summary>
    public FileUploadResult Outcome { get; set; } = FileUploadResult.Stored;

    /// <summary>When true every download throws, as an unreachable file-storage service would.</summary>
    public bool ThrowOnDownload { get; set; }

    /// <summary>Every path a caller fetched, by <see cref="DownloadAsync"/> or <see cref="OpenReadAsync"/> (in order).</summary>
    public List<string> FetchedPaths { get; } = [];

    /// <summary>Every path that was uploaded (in order) — pre-seeded files are not listed.</summary>
    public List<string> UploadedPaths { get; } = [];

    /// <summary>The content type of the last upload to <paramref name="path"/>, or null.</summary>
    public string? ContentTypeOf(string path) => _files.TryGetValue(path, out var f) ? f.ContentType : null;

    /// <summary>The bytes stored under <paramref name="path"/>, or <see langword="null"/> when nothing is.</summary>
    public byte[]? BytesOf(string path) => _files.TryGetValue(path, out var f) ? f.Bytes : null;

    /// <summary>Puts a file into storage without recording an upload.</summary>
    public void Seed(string path, byte[] bytes) => _files[path] = (bytes, "application/octet-stream");

    /// <inheritdoc />
    public Task<byte[]> DownloadAsync(string filePath, string fileStorageBaseUrl, CancellationToken ct = default)
    {
        lock (FetchedPaths) FetchedPaths.Add(filePath);
        return ThrowOnDownload ? throw new IOException("file storage unreachable") : _files.TryGetValue(filePath.TrimStart('/'), out var f) || _files.TryGetValue(filePath, out f)
            ? Task.FromResult(f.Bytes)
            : Task.FromResult(Array.Empty<byte>());
    }

    /// <inheritdoc />
    public Task<Stream> OpenReadAsync(string filePath, string fileStorageBaseUrl, CancellationToken ct = default)
    {
        lock (FetchedPaths) FetchedPaths.Add(filePath);
        return ThrowOnDownload ? throw new IOException("file storage unreachable") : _files.TryGetValue(filePath.TrimStart('/'), out var f)
            ? Task.FromResult<Stream>(new MemoryStream(f.Bytes, writable: false))
            : throw new HttpRequestException("File not found", null, System.Net.HttpStatusCode.NotFound);
    }

    /// <inheritdoc />
    public Task<bool> UploadAsync(byte[] fileData, string contentType, string filePath, string fileStorageBaseUrl, CancellationToken ct = default)
    {
        if (Outcome != FileUploadResult.Stored) return Task.FromResult(false);
        Store(fileData, contentType, filePath);
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<FileUploadResult> UploadWithResultAsync(byte[] fileData, string contentType, string filePath, string fileStorageBaseUrl, CancellationToken ct = default)
    {
        if (Outcome == FileUploadResult.Stored) Store(fileData, contentType, filePath);
        return Task.FromResult(Outcome);
    }

    /// <summary>Stores the file under <paramref name="path"/> (leading slash removed) and records the upload.</summary>
    private void Store(byte[] bytes, string contentType, string path)
    {
        _files[path.TrimStart('/')] = (bytes, contentType);
        lock (UploadedPaths) UploadedPaths.Add(path.TrimStart('/'));
    }

    // -- File Manager ------------------------------------------------------------------

    /// <summary>The bytes received so far of each unfinished File Manager upload.</summary>
    private readonly ConcurrentDictionary<Guid, byte[]> _staging = new();

    /// <summary>What every File Manager upload completion reports; defaults to <see cref="FileUploadResult.Stored"/>.</summary>
    public FileUploadResult FileManagerOutcome { get; set; } = FileUploadResult.Stored;

    /// <summary>When true every File Manager delete fails, as an unreachable file-storage service would.</summary>
    public bool FailFileManagerDeletes { get; set; }

    /// <summary>Every File Manager path a caller asked to delete (in order).</summary>
    public List<string> DeletedPaths { get; } = [];

    /// <summary>Every File Manager upload a caller aborted (in order).</summary>
    public List<Guid> AbortedUploads { get; } = [];

    /// <summary>Whether bytes are being received for <paramref name="uploadId"/>.</summary>
    public bool IsStaging(Guid uploadId) => _staging.ContainsKey(uploadId);

    /// <inheritdoc />
    public async Task<long?> AppendFileManagerChunkAsync(Guid uploadId, long offset, Stream chunk, string fileStorageBaseUrl, CancellationToken ct = default)
    {
        byte[] received = _staging.GetValueOrDefault(uploadId, []);
        if (offset != received.Length) return null;
        using var copy = new MemoryStream();
        await chunk.CopyToAsync(copy, ct);
        _staging[uploadId] = [.. received, .. copy.ToArray()];
        return _staging[uploadId].Length;
    }

    /// <inheritdoc />
    public Task<FileUploadResult> CompleteFileManagerUploadAsync(Guid uploadId, long expectedSize, string fileStorageBaseUrl, CancellationToken ct = default)
    {
        _staging.TryRemove(uploadId, out byte[]? received);
        received ??= [];
        if (received.Length != expectedSize) return Task.FromResult(FileUploadResult.Failed);
        if (FileManagerOutcome == FileUploadResult.Stored) Store(received, "application/octet-stream", $"upload/FileManager/{uploadId:D}");
        return Task.FromResult(FileManagerOutcome);
    }

    /// <inheritdoc />
    public Task<bool> AbortFileManagerUploadAsync(Guid uploadId, string fileStorageBaseUrl, CancellationToken ct = default)
    {
        _staging.TryRemove(uploadId, out _);
        lock (AbortedUploads) AbortedUploads.Add(uploadId);
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<bool> DeleteFileManagerFileAsync(string storagePath, string fileStorageBaseUrl, CancellationToken ct = default)
    {
        lock (DeletedPaths) DeletedPaths.Add(storagePath);
        return Task.FromResult(!FailFileManagerDeletes && _files.TryRemove(storagePath, out _));
    }
}
