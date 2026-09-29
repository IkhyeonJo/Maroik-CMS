using Maroik.Core.Contract.Misc.Enums;

namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// HTTP client interface for communicating with the external Maroik.FileStorage service.
/// File bytes are never stored on the web server; this client proxies them to/from the storage service.
/// </summary>
public interface IFileClient
{
    /// <summary>
    /// Downloads a file from the storage service and returns its raw bytes. Throws on an unsafe
    /// (rooted or <c>..</c>-containing) path, a non-2xx response or a transport error.
    /// </summary>
    /// <param name="filePath">Server-side path of the file within the storage service.</param>
    /// <param name="fileStorageBaseUrl">Base URL of the Maroik.FileStorage service.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<byte[]> DownloadAsync(string filePath, string fileStorageBaseUrl, CancellationToken ct = default);

    /// <summary>
    /// Uploads raw file bytes to the storage service.
    /// Returns true when the service accepts the file (any 2xx); false on a refusal, an error, an
    /// unsafe path, or empty input.
    /// </summary>
    /// <param name="fileData">Raw bytes of the file to upload.</param>
    /// <param name="contentType">MIME type (e.g. "image/png").</param>
    /// <param name="filePath">Destination path including file name within the storage service.</param>
    /// <param name="fileStorageBaseUrl">Base URL of the Maroik.FileStorage service.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<bool> UploadAsync(byte[] fileData, string contentType, string filePath, string fileStorageBaseUrl, CancellationToken ct = default);

    /// <summary>
    /// Same upload as <see cref="UploadAsync"/>, but reports <em>why</em> a refused file was refused —
    /// a virus detection, a scanner outage, or any other failure — for callers that show the user a
    /// specific message.
    /// </summary>
    /// <param name="fileData">Raw bytes of the file to upload.</param>
    /// <param name="contentType">MIME type (e.g. "image/png").</param>
    /// <param name="filePath">Destination path including file name within the storage service.</param>
    /// <param name="fileStorageBaseUrl">Base URL of the Maroik.FileStorage service.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<FileUploadResult> UploadWithResultAsync(byte[] fileData, string contentType, string filePath, string fileStorageBaseUrl, CancellationToken ct = default);
}
