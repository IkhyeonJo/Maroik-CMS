using System.Collections.Concurrent;
using Maroik.Core.Contract.Enums;
using Maroik.Core.Contract.Interfaces;

namespace Maroik.E2E.Tests.Fixtures;

/// <summary>
/// In-memory stand-in for Maroik.FileStorage, shared by both hosts of
/// <see cref="PlaywrightWebApplicationFactory"/> (the E2E run has no file-storage container): files a
/// test seeds can be downloaded and streamed back through the real website, and uploads are kept.
/// </summary>
public sealed class E2EFileStorage : IFileClient
{
    /// <summary>Stored files keyed by path (without a leading slash).</summary>
    private readonly ConcurrentDictionary<string, byte[]> _files = new();

    /// <summary>Puts a file into storage under <paramref name="path"/>.</summary>
    public void Seed(string path, byte[] bytes) => _files[path.TrimStart('/')] = bytes;

    /// <summary>The stored bytes of <paramref name="path"/>; throws like the real service answering 404.</summary>
    private byte[] Read(string path) => _files.TryGetValue(path.Replace('\\', '/').TrimStart('/'), out byte[]? bytes)
        ? bytes
        : throw new HttpRequestException($"File not found: {path}", null, System.Net.HttpStatusCode.NotFound);

    /// <inheritdoc />
    public Task<byte[]> DownloadAsync(string filePath, string fileStorageBaseUrl, CancellationToken ct = default)
        => Task.FromResult(Read(filePath));

    /// <inheritdoc />
    public Task<Stream> OpenReadAsync(string filePath, string fileStorageBaseUrl, CancellationToken ct = default)
        => Task.FromResult<Stream>(new MemoryStream(Read(filePath), writable: false));

    /// <inheritdoc />
    public Task<bool> UploadAsync(byte[] fileData, string contentType, string filePath, string fileStorageBaseUrl, CancellationToken ct = default)
    {
        Seed(filePath, fileData);
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<FileUploadResult> UploadWithResultAsync(byte[] fileData, string contentType, string filePath, string fileStorageBaseUrl, CancellationToken ct = default)
    {
        Seed(filePath, fileData);
        return Task.FromResult(FileUploadResult.Stored);
    }

    /// <summary>The bytes received so far of each unfinished File Manager upload.</summary>
    private readonly ConcurrentDictionary<Guid, byte[]> _staging = new();

    /// <summary>The bytes stored under <paramref name="path"/>, or null when nothing is.</summary>
    public byte[]? BytesOf(string path) => _files.TryGetValue(path.TrimStart('/'), out byte[]? bytes) ? bytes : null;

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
        Seed($"upload/FileManager/{uploadId:D}", received);
        return Task.FromResult(FileUploadResult.Stored);
    }

    /// <inheritdoc />
    public Task<bool> AbortFileManagerUploadAsync(Guid uploadId, string fileStorageBaseUrl, CancellationToken ct = default)
    {
        _staging.TryRemove(uploadId, out _);
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<bool> DeleteFileManagerFileAsync(string storagePath, string fileStorageBaseUrl, CancellationToken ct = default)
        => Task.FromResult(_files.TryRemove(storagePath.TrimStart('/'), out _));
}
