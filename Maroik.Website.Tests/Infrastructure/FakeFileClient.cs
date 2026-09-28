using System.Collections.Concurrent;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Enums;

namespace Maroik.Website.Tests.Infrastructure;

/// <summary>
/// In-memory stand-in for the file-storage service client: uploads are kept in a dictionary (and can be
/// pre-seeded), downloads return what is stored, and the outcome of <see cref="UploadWithResultAsync"/> /
/// <see cref="UploadAsync"/> can be forced so the virus-scan and storage-failure branches are reachable.
/// </summary>
public sealed class FakeFileClient : IFileClient
{
    private readonly ConcurrentDictionary<string, (byte[] Bytes, string ContentType)> _files = new();

    /// <summary>What every upload reports; defaults to <see cref="FileUploadResult.Stored"/>.</summary>
    public FileUploadResult Outcome { get; set; } = FileUploadResult.Stored;

    /// <summary>When true every download throws, as an unreachable file-storage service would.</summary>
    public bool ThrowOnDownload { get; set; }

    /// <summary>Every path that was uploaded (in order) — pre-seeded files are not listed.</summary>
    public List<string> UploadedPaths { get; } = [];

    /// <summary>The content type of the last upload to <paramref name="path"/>, or null.</summary>
    public string? ContentTypeOf(string path) => _files.TryGetValue(path, out var f) ? f.ContentType : null;

    /// <summary>Puts a file into storage without recording an upload.</summary>
    public void Seed(string path, byte[] bytes) => _files[path] = (bytes, "application/octet-stream");

    /// <inheritdoc />
    public Task<byte[]> DownloadAsync(string filePath, string fileStorageBaseUrl, CancellationToken ct = default) =>
        ThrowOnDownload ? throw new IOException("file storage unreachable") : _files.TryGetValue(filePath.TrimStart('/'), out var f) || _files.TryGetValue(filePath, out f)
            ? Task.FromResult(f.Bytes)
            : Task.FromResult(Array.Empty<byte>());

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

    private void Store(byte[] bytes, string contentType, string path)
    {
        _files[path.TrimStart('/')] = (bytes, contentType);
        lock (UploadedPaths) UploadedPaths.Add(path.TrimStart('/'));
    }
}
