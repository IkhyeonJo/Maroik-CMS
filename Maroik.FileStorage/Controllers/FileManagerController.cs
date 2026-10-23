using Maroik.Core.Contract.Messages;
using Maroik.FileStorage.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maroik.FileStorage.Controllers;

/// <summary>
/// Internal API of the File Manager's large-file flow (Implementation note [A]), next to <see cref="FileController"/>, which serves the
/// attachments: <c>chunk</c> appends a 16 MB piece of an upload, <c>complete</c> scans and stores it, <c>abort</c> drops it, and
/// <c>delete</c> removes a stored File Manager file. Like the rest of this service it is reachable only on the internal network.
/// The disk work is delegated to <see cref="IFileManagerStorageService"/>; a refusal is answered with its <c>FileStorageRefusals</c>
/// text and logged with the upload id or path and the caller's correlation id.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class FileManagerController(IFileManagerStorageService storage, ILogger<FileManagerController> logger) : ControllerBase
{
    /// <summary>The fixed answer to a request that failed inside the service (the exception's text never leaves the server).</summary>
    private const string RequestFailed = "The request failed.";

    /// <summary>Appends <paramref name="chunk"/> at <paramref name="offset"/> of upload <paramref name="uploadId"/>; answers the new length.</summary>
    [AllowAnonymous]
    [HttpPost("chunk")]
    public async Task<IActionResult> ChunkAsync([FromForm] string? uploadId, [FromForm] long offset, IFormFile? chunk, [FromForm] string? correlationId = null)
    {
        try
        {
            if (chunk == null)
                return BadRequest(FileStorageRefusals.InvalidUpload);

            await using Stream content = chunk.OpenReadStream();
            (string? error, long length) = await storage.AppendChunkAsync(uploadId, offset, content, HttpContext.RequestAborted);
            if (error == null)
                return Ok(length);

            logger.LogWarning("File Manager chunk refused: {Reason}. UploadId={UploadId} Offset={Offset} Received={Received} CorrelationId={CorrelationId}",
                error, uploadId, offset, length, correlationId);
            return error == FileStorageRefusals.OffsetMismatch ? Conflict(error) : BadRequest(error);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "File Manager chunk failed. UploadId={UploadId} Offset={Offset} CorrelationId={CorrelationId}", uploadId, offset, correlationId);
            return BadRequest(RequestFailed);
        }
    }

    /// <summary>Scans and stores upload <paramref name="uploadId"/> once it has <paramref name="expectedSize"/> bytes.</summary>
    [AllowAnonymous]
    [HttpPost("complete")]
    public async Task<IActionResult> CompleteAsync([FromForm] string? uploadId, [FromForm] long expectedSize, [FromForm] string? correlationId = null)
    {
        try
        {
            string? error = await storage.CompleteAsync(uploadId, expectedSize, HttpContext.RequestAborted);
            if (error == null)
            {
                logger.LogInformation("File Manager upload {UploadId} stored ({Size} bytes). CorrelationId={CorrelationId}", uploadId, expectedSize, correlationId);
                return Ok();
            }

            // A scanner outage is an operational fault; an infected file or a bad upload is the caller's input.
            if (error == FileStorageRefusals.ScanUnavailable)
                logger.LogError("File Manager upload refused: {Reason}. UploadId={UploadId} CorrelationId={CorrelationId}", error, uploadId, correlationId);
            else
                logger.LogWarning("File Manager upload refused: {Reason}. UploadId={UploadId} CorrelationId={CorrelationId}", error, uploadId, correlationId);
            return BadRequest(error);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "File Manager upload completion failed. UploadId={UploadId} CorrelationId={CorrelationId}", uploadId, correlationId);
            return BadRequest(RequestFailed);
        }
    }

    /// <summary>Drops the staging file of upload <paramref name="uploadId"/>.</summary>
    [AllowAnonymous]
    [HttpPost("abort")]
    public IActionResult Abort([FromForm] string? uploadId, [FromForm] string? correlationId = null)
    {
        try
        {
            if (storage.Abort(uploadId))
                return Ok();

            logger.LogWarning("File Manager abort refused: {Reason}. UploadId={UploadId} CorrelationId={CorrelationId}", FileStorageRefusals.InvalidUpload, uploadId, correlationId);
            return BadRequest(FileStorageRefusals.InvalidUpload);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "File Manager abort failed. UploadId={UploadId} CorrelationId={CorrelationId}", uploadId, correlationId);
            return BadRequest(RequestFailed);
        }
    }

    /// <summary>Deletes the stored File Manager file at <paramref name="filePath"/> (P4).</summary>
    [AllowAnonymous]
    [HttpPost("delete")]
    public IActionResult Delete([FromForm] string? filePath, [FromForm] string? correlationId = null)
    {
        try
        {
            string? error = storage.Delete(filePath);
            if (error == null)
            {
                logger.LogInformation("File Manager file deleted: {FilePath}. CorrelationId={CorrelationId}", filePath, correlationId);
                return Ok();
            }

            logger.LogWarning("File Manager delete refused: {Reason}. FilePath={FilePath} CorrelationId={CorrelationId}", error, filePath, correlationId);
            return error == FileStorageRefusals.FileNotFound ? NotFound(error) : BadRequest(error);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "File Manager delete failed. FilePath={FilePath} CorrelationId={CorrelationId}", filePath, correlationId);
            return BadRequest(RequestFailed);
        }
    }
}
