using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Extensions;
using Maroik.Core.Contract.Misc.Settings;
using Maroik.Core.Domain.Account;
using Maroik.Core.Domain.Board;
using Maroik.Core.Domain.Media;
using Maroik.Website.Attributes;
using Maroik.Website.Extensions;
using Maroik.Website.Models;
using Maroik.Website.Models.ViewModels.Forum;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;

namespace Maroik.Website.Controllers;

/// <summary>
/// Manages the free forum: listing, writing, editing, and deleting posts and comments,
/// plus Summernote inline-image uploads.
/// </summary>
public class ForumController(
    IHtmlLocalizer<ForumController> localizer,
    ILogger<ForumController> logger,
    IBoardService boardService,
    IAccountService accountService,
    IRsaService rsa,
    IOptions<ServerSetting> serverSettings) : Controller
{
    /// <summary>Shared extension-to-MIME-type lookup for attachment downloads (safe for concurrent reads).</summary>
    private static readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

    #region FreeForum

    #region Create

    #region Write

    #region FreeBoard
    /// <summary>Submits a new free-forum post (with optional file attachment).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> WriteFreeBoard(BoardInputViewModel boardInputViewModel, CancellationToken ct)
    {
        try
        {
            if (!ModelState.IsValid)
                return Json(new { result = false, error = localizer["Input is invalid"].Value });

            boardInputViewModel.Content ??= "";

            // The DB-fresh, re-validated account ViewBagPopulatorFilter already loaded for this
            // request (via HttpContext.Items) — reuse it instead of re-reading the login-time
            // session snapshot, so a mid-session role change takes effect here immediately.
            AccountResponse loggedInAccount = ViewBag.LoggedInAccount;

            if (loggedInAccount.Role is not (Role.Admin or Role.User))
                return Json(new { result = false, error = localizer["Please Login to write board"].Value });

            BoardRequest boardRequest = new()
            {
                Type = BoardTypes.FreeForum,
                Title = boardInputViewModel.Title,
                Content = boardInputViewModel.Content,
                Writer = loggedInAccount.Nickname!,
                Locked = boardInputViewModel.Locked,
                Noticed = boardInputViewModel.Noticed
            };

            AttachedFileDto? attachedFile = await boardInputViewModel.UploadedFile.ToAttachedFileInfoAsync(serverSettings.Value.MaxAttachedFileSizeBytes, ct);

            // WriteBoardAsync itself gates Noticed on isAdmin, so a non-admin's Noticed=true can
            // never pin a post regardless of what this controller (or any other caller) sends.
            ServiceResult result = await boardService.WriteBoardAsync(boardRequest, loggedInAccount.Role == Role.Admin, attachedFile, ct);
            return result.Success
                ? Json(new { result = true, message = localizer["The board has been successfully created."].Value })
                : Json(new { result = false, error = localizer["Input is invalid"].Value });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception in forum action");
            return Json(new { result = false, error = localizer["Input is invalid"].Value });
        }
    }
    #endregion

    #region FreeComment
    /// <summary>Submits a comment on a free-forum post.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> WriteFreeComment([FromBody] BoardCommentInputViewModel boardCommentInputViewModel, CancellationToken ct)
    {
        try
        {
            if (!ModelState.IsValid)
                return Json(new { result = false, error = localizer["Input is invalid"].Value });

            if (string.IsNullOrEmpty(boardCommentInputViewModel.Content))
                return Json(new { result = false, error = localizer["Please enter a comment."].Value });

            // DB-fresh account already loaded by ViewBagPopulatorFilter for this request (see WriteFreeBoard).
            AccountResponse loggedInAccount = ViewBag.LoggedInAccount;
            if (loggedInAccount.Role is not (Role.Admin or Role.User))
                return Json(new { result = false, error = localizer["Please Login to write comment."].Value });

            BoardCommentRequest commentRequest = new()
            {
                BoardId = boardCommentInputViewModel.BoardId,
                AvatarImagePath = loggedInAccount.AvatarImagePath!,
                Writer = loggedInAccount.Nickname!,
                Content = boardCommentInputViewModel.Content
            };

            ServiceResult result = await boardService.WriteCommentAsync(commentRequest, isAdmin: loggedInAccount.Role == Role.Admin, requiredType: BoardTypes.FreeForum, ct: ct);
            return result.Success
                ? Json(new { result = true, boardId = boardCommentInputViewModel.BoardId, page = boardCommentInputViewModel.DetailCurrentPage })
                : Json(new { result = false, error = localizer["Input is invalid"].Value });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception in forum action");
            return Json(new { result = false, error = localizer["Input is invalid"].Value });
        }
    }
    #endregion

    #endregion

    #region Summernote Image File Upload
    /// <summary>
    /// Validates and stores a Summernote inline image, returning its bytes (base64) and content type for
    /// the editor preview plus its RSA-encrypted storage path, which the editor keeps in the image's <c>alt</c>.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> UploadImageFile(IFormFile? summernoteImageFile, CancellationToken ct)
    {
        if (summernoteImageFile == null)
            return Ok(new { result = false, errorMessage = localizer["Please attach a file."].Value });

        if (summernoteImageFile.Length <= 0 || summernoteImageFile.Length > serverSettings.Value.MaxAttachedFileSizeBytes)
            return Ok(new { result = false, errorMessage = localizer["File Size must be smaller than {0}MB.", serverSettings.Value.MaxAttachedFileSizeBytes / (1024 * 1024)].ToPlainString() });

        var ext = Path.GetExtension(summernoteImageFile.FileName).ToLowerInvariant();
        if (!ImageUploadPolicy.IsAllowedExtension(ext))
            return Ok(new { result = false, errorMessage = localizer["Only .jpg or jpeg or .png file allowed."].Value });

        AttachedFileDto file = (await summernoteImageFile.ToAttachedFileInfoAsync(serverSettings.Value.MaxAttachedFileSizeBytes, ct))!;

        SummernoteUploadResult uploadResult = await boardService.UploadSummernoteImageAsync(file, "Forum", BoardTypes.FreeForum, ct);

        if (!uploadResult.Success)
            return Ok(new { result = false, errorMessage = localizer[uploadResult.ErrorKey ?? "Input is invalid"].Value });

        // The Summernote editor only needs the raw bytes (base64) + content type to build an object
        // URL for the inserted <img>; return those explicitly rather than serializing a whole
        // FileContentResult and relying on its property names.
        return Ok(new
        {
            result = true,
            file = new
            {
                fileContents = Convert.ToBase64String(uploadResult.FileBytes!),
                contentType = uploadResult.ContentType!
            },
            filePath = rsa.Encrypt(uploadResult.FilePath!)
        });
    }
    #endregion

    #endregion

    #region Read

    #region Edit, List, Detail, Write
    /// <summary>Displays the free-forum page in the mode named by <paramref name="method"/>: "list" (default), "write", "detail" or "edit".</summary>
    public async Task<IActionResult> FreeForum(string method = "list", int? boardId = null, int page = 1, string searchType = "", string searchText = "", CancellationToken ct = default)
    {
        switch (method)
        {
            // Write
            case "write":
            {
                FreeForumOutputViewModel freeForumOutputViewModel = new() { Method = method };

                bool isAccountSessionExist = ((AccountResponse)ViewBag.LoggedInAccount).Role != Role.Anonymous;
                return isAccountSessionExist ? View(freeForumOutputViewModel) : RedirectToAction("Login", "Account");
            }
            // Detail view
            case "detail" when boardId == null:
                return RedirectToAction(BoardTypes.FreeForum, "Forum");
            case "detail":
            {
                FreeForumOutputViewModel freeForumOutputViewModel = new()
                {
                    Method = method,
                    DetailCurrentPage = page,
                    DetailBoardId = (int)boardId
                };

                try
                {
                    BoardResponse? freeBoard = await boardService.GetBoardByIdAsync((long)boardId, BoardTypes.FreeForum, ct);
                    BoardAttachedFileDto? boardAttachedFile = await boardService.GetAttachedFileByBoardIdAsync((long)boardId, ct);

                    if (freeBoard == null || freeBoard.Deleted)
                        return RedirectToAction(BoardTypes.FreeForum, "Forum");

                    // ViewBag.LoggedInAccount is re-fetched from the database by ViewBagPopulatorFilter
                    // on every request (not the session-cached snapshot), so a mid-session Role
                    // change (e.g. an admin demotion) is reflected immediately in the CanView check
                    // below — matching the "edit" case's use of ViewBag.LoggedInAccount.
                    AccountResponse? loggedInAccount = ViewBag.LoggedInAccount;
                    if (!boardService.CanView(freeBoard, loggedInAccount))
                        return RedirectToAction(BoardTypes.FreeForum, "Forum");

                    await boardService.IncrementBoardViewAsync((long)boardId, ct);

                    (freeBoard.Content, bool isImgTagIncluded) = await boardService.PrepareHtmlForDisplayAsync(freeBoard.Content ?? "", ct);

                    freeForumOutputViewModel.LoggedInAccount = loggedInAccount ?? new AccountResponse { Role = Role.Anonymous };
                    freeForumOutputViewModel.LoggedInAccountTimeZoneIanaId = freeForumOutputViewModel.LoggedInAccount.TimeZoneIanaId ?? "";

                    freeForumOutputViewModel.BoardOutputViewModel = new BoardOutputViewModel
                    {
                        Title = freeBoard.Title,
                        Writer = freeBoard.Writer,
                        Views = freeBoard.View,
                        Content = freeBoard.Content,
                        Updated = freeBoard.Updated,
                        BoardAttachedFileName = boardAttachedFile?.Name ?? "",
                        BoardAttachedFileExtension = boardAttachedFile?.Extension ?? "",
                        BoardAttachedFileSize = boardAttachedFile?.Size ?? 0,
                        BoardAttachedFilePath = boardAttachedFile?.Path ?? "",
                        IsImgTagIncluded = isImgTagIncluded,
                        CanModify = freeForumOutputViewModel.LoggedInAccount.IsOwner(freeBoard.Writer),
                        CanDelete = freeForumOutputViewModel.LoggedInAccount.IsOwnerOrAdmin(freeBoard.Writer)
                    };

                    if (!string.IsNullOrEmpty(freeForumOutputViewModel.BoardOutputViewModel?.BoardAttachedFilePath ?? ""))
                    {
                        byte[]? fileData = await boardService.DownloadFileAsync(freeForumOutputViewModel.BoardOutputViewModel?.BoardAttachedFilePath ?? "", ct);
                        if (fileData != null)
                        {
                            freeForumOutputViewModel.BoardOutputViewModel?.BoardAttachedFileBase64Data = Convert.ToBase64String(fileData);
                            freeForumOutputViewModel.BoardOutputViewModel?.BoardAttachedFileContentType =
                                _contentTypeProvider.TryGetContentType(freeForumOutputViewModel.BoardOutputViewModel?.BoardAttachedFilePath ?? "", out string? contentType)
                                    ? contentType : "application/octet-stream";
                        }
                    }

                    freeForumOutputViewModel.DetailBoardComments =
                        [.. await boardService.GetCommentsByBoardIdAsync((long)boardId, ct)];
                    var detailNicknames = freeForumOutputViewModel.DetailBoardComments.Select(c => c.Writer)
                        .Append(freeBoard.Writer ?? "")
                        .Where(w => !string.IsNullOrEmpty(w))
                        .Select(w => w!)
                        .Distinct();
                    freeForumOutputViewModel.AllAccounts = await accountService.GetAccountsByNicknamesAsync(detailNicknames, ct);
                    freeForumOutputViewModel.AdminNicknames = freeForumOutputViewModel.AllAccounts.ToAdminNicknameSet();

                    return View(freeForumOutputViewModel);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unhandled exception in FreeForum action");
                    return RedirectToAction(BoardTypes.FreeForum, "Forum");
                }
            }
            // Edit
            case "edit" when boardId == null:
                return RedirectToAction(BoardTypes.FreeForum, "Forum");
            case "edit":
            {
                FreeForumOutputViewModel freeForumOutputViewModel = new()
                {
                    Method = method,
                    EditCurrentPage = page,
                    EditBoardId = (int)boardId
                };

                try
                {
                    BoardResponse? freeBoard = await boardService.GetBoardByIdAsync((long)boardId, BoardTypes.FreeForum, ct);
                    BoardAttachedFileDto? boardAttachedFile = await boardService.GetAttachedFileByBoardIdAsync((long)boardId, ct);

                    if (freeBoard == null || freeBoard.Deleted)
                        return RedirectToAction(BoardTypes.FreeForum, "Forum");

                    // ViewBag.LoggedInAccount is re-fetched from the database by ViewBagPopulatorFilter
                    // on every request (not the session-cached snapshot), so ownership is always checked
                    // against the account's current state. The edit page is owner-only (no admin bypass).
                    AccountResponse loggedInAccount = ViewBag.LoggedInAccount;
                    if (!loggedInAccount.IsOwner(freeBoard.Writer))
                        return RedirectToAction(BoardTypes.FreeForum, "Forum");

                    (freeBoard.Content, bool isImgTagIncluded) = await boardService.PrepareHtmlForDisplayAsync(freeBoard.Content ?? "", ct);

                    freeForumOutputViewModel.BoardOutputViewModel = new BoardOutputViewModel
                    {
                        Title = freeBoard.Title,
                        Writer = freeBoard.Writer,
                        Views = freeBoard.View,
                        Content = freeBoard.Content,
                        Locked = freeBoard.Locked,
                        Updated = freeBoard.Updated,
                        BoardAttachedFileName = boardAttachedFile?.Name ?? "",
                        BoardAttachedFileExtension = boardAttachedFile?.Extension ?? "",
                        BoardAttachedFileSize = boardAttachedFile?.Size ?? 0,
                        BoardAttachedFilePath = boardAttachedFile?.Path ?? "",
                        IsImgTagIncluded = isImgTagIncluded
                    };

                    if (string.IsNullOrEmpty(freeForumOutputViewModel.BoardOutputViewModel?.BoardAttachedFilePath ?? ""))
                    {
                        return View(freeForumOutputViewModel);
                    }

                    byte[]? fileData = await boardService.DownloadFileAsync(freeForumOutputViewModel.BoardOutputViewModel?.BoardAttachedFilePath ?? "", ct);
                    if (fileData == null)
                    {
                        return View(freeForumOutputViewModel);
                    }

                    freeForumOutputViewModel.BoardOutputViewModel?.BoardAttachedFileBase64Data = Convert.ToBase64String(fileData);
                    freeForumOutputViewModel.BoardOutputViewModel?.BoardAttachedFileContentType =
                        _contentTypeProvider.TryGetContentType(freeForumOutputViewModel.BoardOutputViewModel?.BoardAttachedFilePath ?? "", out string? contentType)
                            ? contentType : "application/octet-stream";

                    return View(freeForumOutputViewModel);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unhandled exception in FreeForum action");
                    return RedirectToAction(BoardTypes.FreeForum, "Forum");
                }
            }
            // List
            default:
            {
                const int pageSize = 5;
                if (page < 1) page = 1;

                // ViewBag.LoggedInAccount is re-fetched from the database by ViewBagPopulatorFilter
                // on every request (not the session-cached snapshot), so a mid-session Role change
                // is reflected immediately in the locked-post visibility rule below — same
                // rationale as the "detail" case above.
                AccountResponse loggedInAccountForQuery = ViewBag.LoggedInAccount;
                bool isLoggedIn = loggedInAccountForQuery.Role != Role.Anonymous;
                // Filtering, search/visibility rules, ordering, and paging are all pushed down into
                // one SQL query (see BoardRepository.QueryPageAsync) instead of materializing the
                // whole board-type bucket into memory first.
                (IEnumerable<BoardResponse> pageItems, int totalCount) = await boardService.GetBoardPageAsync(new BoardPageQuery(
                    Type: BoardTypes.FreeForum,
                    OwnerNickname: null,
                    SearchType: searchType,
                    SearchText: searchText,
                    IsLoggedIn: isLoggedIn,
                    ViewerRole: isLoggedIn ? loggedInAccountForQuery.Role : null,
                    ViewerNickname: isLoggedIn ? loggedInAccountForQuery.Nickname : null,
                    Page: page,
                    PageSize: pageSize), ct);

                FreeForumOutputViewModel freeForumOutputViewModel = new()
                {
                    NoticeBoards = await boardService.GetNoticedBoardsAsync(BoardTypes.FreeForum, ct),
                    Method = "list",
                    Pager = new Pager(totalCount, page, pageSize),
                    Boards =
                        [.. pageItems],
                    LoggedInAccount = loggedInAccountForQuery
                };

                freeForumOutputViewModel.LoggedInAccountTimeZoneIanaId = freeForumOutputViewModel.LoggedInAccount.TimeZoneIanaId ?? "";
                var boardResponses = freeForumOutputViewModel.Boards as BoardResponse[] ?? [.. freeForumOutputViewModel.Boards];
                var listNicknames = freeForumOutputViewModel.NoticeBoards.Select(b => b.Writer)
                    .Concat(boardResponses.Select(b => b.Writer))
                    .Where(w => !string.IsNullOrEmpty(w))
                    .Select(w => w!)
                    .Distinct();
                freeForumOutputViewModel.AllAccounts = await accountService.GetAccountsByNicknamesAsync(listNicknames, ct);
                freeForumOutputViewModel.AdminNicknames = freeForumOutputViewModel.AllAccounts.ToAdminNicknameSet();

                List<long> allBoardIds =
                [
                    .. freeForumOutputViewModel.NoticeBoards.Select(b => b.Id)
                        .Concat(boardResponses.Select(b => b.Id))
                        .Distinct()
                ];

                Dictionary<long, int> commentCounts = await boardService.GetCommentCountsForBoardsAsync(allBoardIds, ct);
                Dictionary<long, BoardAttachedFileDto?> attachedFiles = await boardService.GetAttachedFilesForBoardsAsync(allBoardIds, ct);

                foreach (var board in freeForumOutputViewModel.NoticeBoards)
                {
                    freeForumOutputViewModel.CommentCountByBoardId[board.Id] = commentCounts.GetValueOrDefault(board.Id, 0);
                    BoardAttachedFileDto? af = attachedFiles.GetValueOrDefault(board.Id);
                    freeForumOutputViewModel.AttachedFilesByBoardId[board.Id] = af != null && !string.IsNullOrEmpty(af.Name) ? [af] : [];
                }
                
                foreach (var board in boardResponses)
                {
                    freeForumOutputViewModel.CommentCountByBoardId[board.Id] = commentCounts.GetValueOrDefault(board.Id, 0);
                    BoardAttachedFileDto? af = attachedFiles.GetValueOrDefault(board.Id);
                    freeForumOutputViewModel.AttachedFilesByBoardId[board.Id] = af != null && !string.IsNullOrEmpty(af.Name) ? [af] : [];
                }

                freeForumOutputViewModel.SelectedSearchType = searchType;
                freeForumOutputViewModel.TypedSearchText = searchText;
                return View(freeForumOutputViewModel);
            }
        }
    }
    #endregion

    #region IsBoardExists
    /// <summary>
    /// Checks that a free-forum post still exists and is visible to the caller (a locked post only to its
    /// author or an admin). Called by the client before confirming a delete.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> IsBoardExists(int id, CancellationToken ct)
    {
        try
        {
            BoardResponse? board = await boardService.GetBoardByIdAsync(id, BoardTypes.FreeForum, ct);
            if (board == null || board.Deleted)
                return Json(new { result = false, error = localizer["Input is invalid"].Value });

            if (!board.Locked)
            {
                return Json(new { result = true, freeBoard = board });
            }

            // Re-fetched from the database by ViewBagPopulatorFilter on every request.
            AccountResponse loggedInAccount = ViewBag.LoggedInAccount;
            if (!loggedInAccount.IsOwnerOrAdmin(board.Writer))
                return Json(new { result = false, error = localizer["Input is invalid"].Value });

            return Json(new { result = true, freeBoard = board });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception in forum action");
            return Json(new { result = false, error = localizer["Input is invalid"].Value });
        }
    }
    #endregion

    #endregion

    #region Update

    #region Edit
    /// <summary>Saves edits to an existing free-forum post.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> EditFreeBoard(BoardInputViewModel boardInputViewModel, CancellationToken ct)
    {
        try
        {
            if (!ModelState.IsValid)
                return Json(new { result = false, error = localizer["Input is invalid"].Value });

            boardInputViewModel.Content ??= "";

            // DB-fresh account already loaded by ViewBagPopulatorFilter for this request (see WriteFreeBoard).
            AccountResponse loggedInAccount = ViewBag.LoggedInAccount;
            if (loggedInAccount.Role is not (Role.Admin or Role.User))
                return Json(new { result = false, error = localizer["Please Login to edit board."].Value });

            BoardRequest boardRequest = new()
            {
                Id = boardInputViewModel.Id,
                Type = BoardTypes.FreeForum,
                Title = boardInputViewModel.Title,
                Content = boardInputViewModel.Content,
                Locked = boardInputViewModel.Locked
            };

            AttachedFileDto? attachedFile = await boardInputViewModel.UploadedFile.ToAttachedFileInfoAsync(serverSettings.Value.MaxAttachedFileSizeBytes, ct);

            ServiceResult result = await boardService.EditBoardAsync(boardRequest, loggedInAccount.Nickname!, loggedInAccount.Role == Role.Admin, attachedFile, ct);
            return result.Success
                ? Json(new { result = true, message = localizer["The board has been successfully updated."].Value })
                : Json(new { result = false, error = localizer["Input is invalid"].Value });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception in forum action");
            return Json(new { result = false, error = localizer["Input is invalid"].Value });
        }
    }
    #endregion

    #endregion

    #region Delete

    #region Board
    /// <summary>Soft-deletes a free-forum post.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> DeleteBoard([FromBody] BoardInputViewModel boardInputViewModel, CancellationToken ct)
    {
        try
        {
            // Re-fetched from the database by ViewBagPopulatorFilter on every request.
            AccountResponse loggedInAccount = ViewBag.LoggedInAccount;

            ServiceResult result = await boardService.DeleteBoardAsync(
                boardInputViewModel.Id,
                BoardTypes.FreeForum,
                loggedInAccount.Nickname!,
                loggedInAccount.Role == Role.Admin,
                ct);

            return result.Success
                ? Json(new { result = true, message = localizer["The board has been successfully deleted."].Value })
                : Json(new { result = false, error = localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception in forum action");
            return Json(new { result = false, error = localizer["Input is invalid"].Value });
        }
    }
    #endregion

    #region BoardComment
    /// <summary>Soft-deletes a free-forum comment.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> DeleteComment(int id, CancellationToken ct)
    {
        try
        {
            // Re-fetched from the database by ViewBagPopulatorFilter on every request.
            AccountResponse loggedInAccount = ViewBag.LoggedInAccount;

            ServiceResult result = await boardService.DeleteCommentAsync(
                id,
                loggedInAccount.Nickname!,
                loggedInAccount.Role == Role.Admin,
                BoardTypes.FreeForum,
                ct);

            return result.Success
                ? Json(new { result = true })
                : Json(new { result = false, error = localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception in forum action");
            return Json(new { result = false, error = localizer["Input is invalid"].Value });
        }
    }
    #endregion

    #endregion

    #endregion
}
