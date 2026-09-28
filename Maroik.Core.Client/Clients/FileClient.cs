using System.Diagnostics;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Enums;
using Maroik.Core.Contract.Misc.Messaging;
using Microsoft.Extensions.Logging;

namespace Maroik.Core.Client.Clients;

/// <summary>
/// Concrete implementation of <see cref="IFileClient"/> that communicates with the
/// Maroik.FileStorage microservice via HTTP multipart/form-data requests.
/// </summary>
public class FileClient(IHttpClientFactory httpClientFactory, ILogger<FileClient> logger) : IFileClient
{
    /// <summary>
    /// Rejects a storage path that is rooted or contains a <c>..</c> segment, so a value that ever
    /// reaches here from a less-trusted source can't be used to escape the storage root.
    /// The app's own paths are always relative (e.g. <c>upload/Forum/.../file.zip</c>).
    /// </summary>
    private static bool IsSafeRelativePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        // Normalize separators before checking rootedness/traversal, not after: a value starting
        // with '\' is not rooted by Path.IsPathRooted's rules on this (Linux) runtime, but becomes
        // a rooted Unix path ("/etc/passwd") the moment it is normalized for the storage service
        // (see DownloadAsync/UploadAsync below) — checking the un-normalized string would let that
        // form slip past this guard.
        string normalized = filePath.Replace('\\', '/');
        return !normalized.StartsWith('/')
            && !Path.IsPathRooted(normalized)
            && !normalized.Split('/').Contains("..");
    }

    /// <inheritdoc />
    public async Task<byte[]> DownloadAsync(string filePath, string fileStorageBaseUrl, CancellationToken ct = default)
    {
        if (!IsSafeRelativePath(filePath))
            throw new ArgumentException($"Unsafe storage path: '{filePath}'.", nameof(filePath));

        var correlationId = Activity.Current?.Id ?? "";
#pragma warning disable CA1873
        logger.LogInformation("Requesting file download: {FilePath}. CorrelationId={CorrelationId}", filePath, correlationId);
#pragma warning restore CA1873

        try
        {
            using HttpClient httpClient = httpClientFactory.CreateClient();
            using MultipartFormDataContent content = [];

            // Pass the server-side file path to the storage service. Separators are normalized to
            // "/": the storage service runs on Linux and treats "\" as a literal filename
            // character, so a path built on Windows must not be forwarded with backslashes.
            content.Add(new StringContent(filePath.Replace('\\', '/')), "filePath");
            // So Maroik.FileStorage's failure logs for this call can be traced back to this request.
            content.Add(new StringContent(correlationId), "correlationId");

            HttpResponseMessage response = await httpClient.PostAsync($"{fileStorageBaseUrl}/api/File/download", content, ct);

            _ = response.EnsureSuccessStatusCode(); // Throw on non-2xx status
            return await response.Content.ReadAsByteArrayAsync(ct); // Return raw file bytes
        }
        catch (Exception ex)
        {
            // Parity with UploadAsync: log with the correlation id, then rethrow (callers of the
            // download path already handle the exception and this method has no "false" to return).
            logger.LogError(ex, "File download request failed for {FilePath}. CorrelationId={CorrelationId}", filePath, correlationId);
            throw;
        }
    }

    /// <summary>The storage service's plain-text refusal reason, truncated; empty if it cannot be read.</summary>
    private async Task<string> ReadReasonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            string body = await response.Content.ReadAsStringAsync(ct);
            return body.Length <= 200 ? body : body[..200];
        }
        catch (Exception e)
        {
            logger.LogDebug(e, "The file-storage refusal response body could not be read");
            return "";
        }
    }

    /// <inheritdoc />
    public async Task<bool> UploadAsync(byte[]? fileData, string contentType, string filePath, string fileStorageBaseUrl, CancellationToken ct = default)
        => await UploadWithResultAsync(fileData, contentType, filePath, fileStorageBaseUrl, ct) == FileUploadResult.Stored;

    /// <summary>Maps the storage service's refusal reason back to a <see cref="FileUploadResult"/>.</summary>
    private static FileUploadResult ClassifyRefusal(string reason)
    {
        if (reason.Contains(FileStorageRefusals.Infected, StringComparison.Ordinal))
            return FileUploadResult.Infected;
        if (reason.Contains(FileStorageRefusals.ScanUnavailable, StringComparison.Ordinal))
            return FileUploadResult.ScanUnavailable;
        return FileUploadResult.Failed;
    }

    /// <inheritdoc />
    public async Task<FileUploadResult> UploadWithResultAsync(byte[]? fileData, string contentType, string filePath, string fileStorageBaseUrl, CancellationToken ct = default)
    {
        try
        {
            // Strip any directory component from the file name to prevent path-traversal attacks
            string sanitizedFileName = Path.GetFileName(filePath);

            // Reject empty uploads immediately
            if (fileData is not { Length: > 0 })
            {
                return FileUploadResult.Failed;
            }

            // The directory portion is forwarded to the storage service as-is; reject a rooted or
            // ".."-containing path so it can't place the file outside the storage root.
            if (!IsSafeRelativePath(filePath))
            {
                logger.LogWarning("Rejected file upload with unsafe storage path: {FilePath}", filePath);
                return FileUploadResult.Failed;
            }

            var correlationId = Activity.Current?.Id ?? "";
#pragma warning disable CA1873
            logger.LogInformation("Requesting file upload: {FilePath}. CorrelationId={CorrelationId}", filePath, correlationId);
#pragma warning restore CA1873

            using HttpClient httpClient = httpClientFactory.CreateClient();
            using MultipartFormDataContent content = [];

            // Wrap raw bytes with the correct MIME type
            ByteArrayContent fileContent = new(fileData);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

            // Attach the file bytes under the "file" field name
            content.Add(fileContent, "file", sanitizedFileName);

            // Tell the storage service where to save the file (directory path only), separators
            // normalized to "/" — on Linux the service would otherwise create a single directory
            // literally named "upload\Area\...".
            content.Add(new StringContent((Path.GetDirectoryName(filePath) ?? "").Replace('\\', '/')), "filePath");
            // So Maroik.FileStorage's failure logs for this call can be traced back to this request.
            content.Add(new StringContent(correlationId), "correlationId");

            HttpResponseMessage response = await httpClient.PostAsync($"{fileStorageBaseUrl}/api/File/upload", content, ct);

            // Any 2xx means the storage service accepted the file — don't treat a 201/204 as a
            // failure (which would make the caller roll back over a file that was actually stored).
            if (response.IsSuccessStatusCode)
            {
                return FileUploadResult.Stored;
            }

            // The storage service says why it refused (virus found, could not be scanned, bad
            // extension, ...) in the response body; without it the log only shows a bare 400.
            string reason = await ReadReasonAsync(response, ct);
            logger.LogWarning("File upload returned {StatusCode} for {FilePath}: {Reason}", (int)response.StatusCode, filePath, reason);
            return ClassifyRefusal(reason);

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "File upload request failed for {FilePath}", filePath);
            return FileUploadResult.Failed;
        }
    }
}
