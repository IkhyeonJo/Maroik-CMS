using System.Collections.Concurrent;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Enums;

namespace Maroik.E2E.Tests.Infrastructure;

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
}
