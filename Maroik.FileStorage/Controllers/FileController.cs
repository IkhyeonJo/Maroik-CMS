using Maroik.Core.Contract.Misc.Messaging;
using Maroik.FileStorage.Contracts;
using Maroik.FileStorage.Helpers;
using Maroik.FileStorage.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;

namespace Maroik.FileStorage.Controllers;

/// <summary>
/// Internal API controller for the Maroik.FileStorage microservice.
/// Handles virus-scanned file uploads (ClamAV) and raw file downloads.
/// All validation and file-system logic is delegated to <see cref="IFileValidationService"/>.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class FileController(IFileValidationService fileValidationService, IOptions<FileStorageSetting> settings, ILogger<FileController> logger) : ControllerBase
{
    /// <summary>
    /// Shared extension-to-MIME-type lookup. Its constructor builds a ~400-entry default map;
    /// instantiating it per request is wasted work, and the type is documented as safe for concurrent reads.
    /// </summary>
    private static readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

    /// <summary>
    /// Accepts a multipart file upload and delegates validation, virus scan, and persistence
    /// to <see cref="IFileValidationService"/>.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("upload")]
    public async Task<IActionResult> UploadAsync([FromForm] IFormFile file, [FromForm] string filePath, [FromForm] string? correlationId = null)
    {
        try
        {
            var (success, error) = await fileValidationService.ValidateAndSaveAsync(file, filePath);
            if (!success)
            {
                // A scanner outage is an operational fault; every other refusal is the caller's input
                // (a virus, a traversal path, a bad extension …) — both are recorded with the exact reason.
                if (error == FileStorageRefusals.ScanUnavailable)
                    logger.LogError("File upload refused: {Reason}. FilePath={FilePath} CorrelationId={CorrelationId}", error, filePath, correlationId);
                else
                    logger.LogWarning("File upload refused: {Reason}. FilePath={FilePath} CorrelationId={CorrelationId}", error, filePath, correlationId);
                return BadRequest(error);
            }

#pragma warning disable CA1873
            logger.LogInformation("File uploaded: {FilePath}. CorrelationId={CorrelationId}", filePath, correlationId);
#pragma warning restore CA1873
            return Ok("File uploaded successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "File upload failed for {FilePath}. CorrelationId={CorrelationId}", filePath, correlationId);
            return BadRequest("File upload failed.");
        }
    }

    /// <summary>Streams the file at the given server-side <paramref name="filePath"/> back to the caller.</summary>
    [AllowAnonymous]
    [HttpPost("download")]
    public IActionResult Download([FromForm] string filePath, [FromForm] string? correlationId = null)
    {
        try
        {
            var sanitizedFileName = Path.GetFileName(filePath);

            // Confine the read to the storage root. Callers only ever pass server-generated relative
            // paths under "upload/"; a value that resolves outside the root is rejected here.
            var resolvedPath = StoragePath.ResolveWithinRoot(settings.Value.StorageRootPath, filePath);
            if (resolvedPath == null)
            {
                logger.LogWarning("File download refused: path outside the storage root. FilePath={FilePath} CorrelationId={CorrelationId}", filePath, correlationId);
                return NotFound("File not found");
            }

            if (!System.IO.File.Exists(resolvedPath))
            {
                logger.LogWarning("File download failed: file not found. FilePath={FilePath} CorrelationId={CorrelationId}", filePath, correlationId);
                return NotFound("File not found");
            }

            // Streamed straight from disk (not read into memory first). Opened here, inside the try,
            // so an unreadable file is still answered as "Invalid file" below.
#pragma warning disable SCS0018
            var fileStream = new FileStream(resolvedPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
#pragma warning restore SCS0018

            if (!_contentTypeProvider.TryGetContentType(sanitizedFileName, out var contentType))
                contentType = "application/octet-stream";

#pragma warning disable CA1873
            logger.LogInformation("File downloaded: {FilePath}. CorrelationId={CorrelationId}", filePath, correlationId);
#pragma warning restore CA1873
            return File(fileStream, contentType, sanitizedFileName, enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "File download failed for {FilePath}. CorrelationId={CorrelationId}", filePath, correlationId);
            return BadRequest("Invalid file");
        }
    }
}
