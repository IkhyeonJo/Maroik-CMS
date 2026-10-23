using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Accounts;
using Maroik.Core.Domain.FileManagers;
using Maroik.Website.Attributes;
using Maroik.Website.Extensions;
using Maroik.Website.Mappers;
using Maroik.Website.Models.ViewModels.FileManagers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;

namespace Maroik.Website.Controllers;

/// <summary>
/// The File Manager ("My Drive", menu entry <c>/FileManager/UserIndex</c>, User role only): one drive per user of logical folders and
/// files stored by the file-storage service. The page is rendered by the menu GET; everything else is a POST (the menu is the
/// access-control list, so downloads and previews are POSTs too). The rules are the domain's and the orchestration the service's; this
/// controller binds the input, shapes the replies for the page script, and answers every failure as <c>{ result, error }</c>.
/// </summary>
/// <param name="localizer">Localizer for this controller's user-facing messages.</param>
/// <param name="logger">Logger for unexpected failures in the File Manager actions.</param>
/// <param name="fileManagerService">File Manager use cases.</param>
public class FileManagerController(
    IHtmlLocalizer<FileManagerController> localizer,
    ILogger<FileManagerController> logger,
    IFileManagerService fileManagerService) : Controller
{
    #region Page

    /// <summary>Returns the File Manager page; the drive and the folders are fetched by the page script.</summary>
    [HttpGet]
    public IActionResult UserIndex() => View();

    #endregion

    #region Read

    /// <summary>The signed-in account's email.</summary>
    private string Email => HttpContext.GetLoggedInAccount().Email!;

    /// <summary>The signed-in account's time zone.</summary>
    private string TimeZone => HttpContext.GetLoggedInAccount().TimeZoneIanaId ?? "UTC";

    /// <summary>The localized name of the drive root.</summary>
    private string RootName => localizer["My Drive"].Value;

    /// <summary>The folder tree and the storage usage (the left panel).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> GetDrive(CancellationToken ct)
    {
        try
        {
            FileManagerDriveResponse drive = await fileManagerService.GetDriveAsync(Email, ct);
            return Json(new { result = true, drive = drive.ToViewModel(TimeZone, RootName) });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load the File Manager drive of {Email}", Email);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    /// <summary>An open folder's breadcrumb and items.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> GetFolder([FromBody] FileManagerFolderInputViewModel input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Json(new { result = false, error = localizer["Input is invalid"].Value });
        try
        {
            var (result, folder) = await fileManagerService.GetFolderAsync(Email, input.FolderId, ct);
            return result.Success ? Json(new { result = true, folder = folder!.ToViewModel(TimeZone, RootName) }) : Json(new { result = false, error = localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to open File Manager folder {FolderId} of {Email}", input.FolderId, Email);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    /// <summary>The items whose name contains the query, with their locations.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> Search([FromBody] FileManagerSearchInputViewModel input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Json(new { result = false, error = localizer["Input is invalid"].Value });
        try
        {
            List<FileManagerItemResponse> items = await fileManagerService.SearchAsync(Email, input.Query, ct);
            return Json(new { result = true, items = items.Select(i => i.ToViewModel(TimeZone, RootName, withLocation: true)) });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to search the File Manager drive of {Email}", Email);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    /// <summary>The files a multi-download fetches (each selected file, and every file under each selected folder).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> ListDownloadFiles([FromBody] FileManagerSelectionInputViewModel input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Json(new { result = false, error = localizer["Input is invalid"].Value });
        try
        {
            var (result, files) = await fileManagerService.ListFilesForDownloadAsync(Email, input.Ids, ct);
            return result.Success ? Json(new { result = true, files = files.Select(f => new { id = f.Id, name = f.Name }) }) : Json(new { result = false, error = localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list the File Manager files to download for {Email}", Email);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    /// <summary>Streams a file as an attachment (S6); a refusal comes back as JSON.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> Download(long id, CancellationToken ct)
    {
        try
        {
            var (result, file) = await fileManagerService.OpenFileAsync(Email, id, ct);
            // application/octet-stream whatever the type (with the global nosniff), so the browser saves it instead of rendering it.
            return result.Success ? File(file!.Content, "application/octet-stream", file.FileName) : Json(new { result = false, error = localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download File Manager file {ItemId} of {Email}", id, Email);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    /// <summary>An image to preview, with the Content-Type its bytes proved (F9, S6); a refusal comes back as JSON.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> Preview(long id, CancellationToken ct)
    {
        try
        {
            var (result, preview) = await fileManagerService.OpenPreviewAsync(Email, id, ct);
            return result.Success ? File(preview!.Bytes, preview.ContentType) : Json(new { result = false, error = localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to preview File Manager file {ItemId} of {Email}", id, Email);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    #endregion

    #region Folder writes

    /// <summary>Creates a folder (or, for a folder upload, reuses one of the same name).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> CreateFolder([FromBody] FileManagerCreateFolderInputViewModel input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Json(new { result = false, error = localizer["Input is invalid"].Value });
        try
        {
            var (result, folderId) = await fileManagerService.CreateFolderAsync(Email, input.ParentId, input.Name, input.Merge, ct);
            return result.Success
                ? Json(new { result = true, message = localizer["The folder has been created."].Value, folderId })
                : Json(new { result = false, error = localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create a File Manager folder for {Email}", Email);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    /// <summary>Renames an item.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> Rename([FromBody] FileManagerRenameInputViewModel input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Json(new { result = false, error = localizer["Input is invalid"].Value });
        try
        {
            ServiceResult result = await fileManagerService.RenameAsync(Email, input.Id, input.Name, ct);
            return result.Success ? Json(new { result = true, message = localizer["The name has been changed."].Value }) : Json(new { result = false, error = localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to rename File Manager item {ItemId} of {Email}", input.Id, Email);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    /// <summary>Moves the selected items into a folder.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> Move([FromBody] FileManagerSelectionInputViewModel input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Json(new { result = false, error = localizer["Input is invalid"].Value });
        try
        {
            ServiceResult result = await fileManagerService.MoveAsync(Email, input.Ids, input.TargetFolderId, ct);
            return result.Success ? Json(new { result = true, message = localizer["The items have been moved."].Value }) : Json(new { result = false, error = localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to move File Manager items of {Email}", Email);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    /// <summary>Deletes the selected items (a folder with everything under it).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> Delete([FromBody] FileManagerSelectionInputViewModel input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Json(new { result = false, error = localizer["Input is invalid"].Value });
        try
        {
            ServiceResult result = await fileManagerService.DeleteAsync(Email, input.Ids, ct);
            return result.Success ? Json(new { result = true, message = localizer["The items have been deleted."].Value }) : Json(new { result = false, error = localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete File Manager items of {Email}", Email);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    #endregion

    #region Upload

    /// <summary>Whether a batch of the given total size fits the remaining quota (asked before a batch starts).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> CheckQuota([FromBody] FileManagerQuotaInputViewModel input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Json(new { result = false, error = localizer["Input is invalid"].Value });
        try
        {
            ServiceResult result = await fileManagerService.CheckQuotaAsync(Email, input.TotalBytes, ct);
            return result.Success ? Json(new { result = true }) : Json(new { result = false, error = localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check the File Manager quota of {Email}", Email);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    /// <summary>Starts one file's upload: a name conflict to decide, or the upload id and chunk size.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> BeginUpload([FromBody] FileManagerUploadInputViewModel input, CancellationToken ct)
    {
        if (!ModelState.IsValid || !Enum.TryParse(input.Conflict, ignoreCase: false, out FileManagerConflictChoice conflict) || !Enum.IsDefined(conflict)
            || int.TryParse(input.Conflict, out _))
            return Json(new { result = false, error = localizer["Input is invalid"].Value });
        try
        {
            var (result, start) = await fileManagerService.BeginUploadAsync(Email, new FileManagerUploadRequest
            {
                ParentId = input.ParentId, Name = input.Name, Size = input.Size, Conflict = conflict
            }, ct);
            return result.Success
                ? Json(new { result = true, conflict = start!.Conflict, uploadId = start.UploadId.ToString("D"), name = start.Name, chunkBytes = start.ChunkBytes })
                : Json(new { result = false, error = localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start a File Manager upload for {Email}", Email);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    /// <summary>Receives one chunk of an upload (a multipart form) and forwards it to file storage.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> UploadChunk([FromForm] FileManagerChunkInputViewModel input, CancellationToken ct)
    {
        if (!ModelState.IsValid || input.Chunk == null) return Json(new { result = false, error = localizer["Input is invalid"].Value });
        try
        {
            await using Stream chunk = input.Chunk.OpenReadStream();
            var (result, length) = await fileManagerService.AppendChunkAsync(Email, input.UploadId, input.Offset, chunk, input.Chunk.Length, ct);
            return result.Success ? Json(new { result = true, length }) : Json(new { result = false, error = localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to receive a chunk of File Manager upload {UploadId} of {Email}", input.UploadId, Email);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    /// <summary>Completes an upload: scanned and stored, it becomes a file of the drive.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> CompleteUpload([FromBody] FileManagerUploadIdInputViewModel input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Json(new { result = false, error = localizer["Input is invalid"].Value });
        try
        {
            ServiceResult result = await fileManagerService.CompleteUploadAsync(Email, input.UploadId, input.Overwrite, ct);
            return result.Success ? Json(new { result = true, message = localizer["The file has been uploaded."].Value }) : Json(new { result = false, error = localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to complete File Manager upload {UploadId} of {Email}", input.UploadId, Email);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    /// <summary>Abandons an upload.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> AbortUpload([FromBody] FileManagerUploadIdInputViewModel input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Json(new { result = false, error = localizer["Input is invalid"].Value });
        try
        {
            ServiceResult result = await fileManagerService.AbortUploadAsync(Email, input.UploadId, ct);
            return result.Success ? Json(new { result = true }) : Json(new { result = false, error = localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to abort File Manager upload {UploadId} of {Email}", input.UploadId, Email);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    #endregion
}
