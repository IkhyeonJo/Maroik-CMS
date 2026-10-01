using System.Data;
using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Settings;
using Maroik.Core.Domain.Account;
using Maroik.Core.Domain.Board;
using Maroik.Core.Service.Mappers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Maroik.Core.Service.Services;

/// <summary>
/// Implementation of <see cref="IBoardService"/> for forum post / private note management.
/// Zip attachments are checked against <c>AttachmentUploadPolicy</c> and stored/retrieved via the
/// file-storage microservice (<see cref="IFileClient"/>, which virus-scans them with ClamAV).
/// Summernote inline images go through <see cref="IAttachmentContentService"/>: image-validated,
/// stored in the file-storage service, and referenced from the HTML by their RSA-encrypted storage
/// path in <c>alt</c> (embedded as base64 only when the post is rendered). Post/comment write
/// operations use <see cref="IUnitOfWork"/> transactions to keep post and attachment records in sync.
/// </summary>
public class BoardService(
    IBoardRepository boardRepository,
    IBoardAttachedFileRepository boardAttachedFileRepository,
    IBoardCommentRepository boardCommentRepository,
    IFileClient fileClient,
    IUnitOfWork unitOfWork,
    IAttachmentContentService attachmentContent,
    IOptions<ServerSetting> settings,
    ILogger<BoardService> logger,
    TimeProvider timeProvider) : IBoardService
{
    /// <summary>
    /// Maps each board type name to its corresponding file-storage area folder name.
    /// Used when building attachment storage paths.
    /// </summary>
    private static readonly Dictionary<string, string> _boardAreaMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [BoardTypes.FreeForum] = "Forum",
        [BoardTypes.PrivateNote] = "Management"
    };

    /// <inheritdoc />
    public async Task<(IEnumerable<BoardResponse> Items, int TotalCount)> GetBoardPageAsync(BoardPageQuery query, CancellationToken ct = default)
    {
        // REPEATABLE READ pins the count and page reads QueryPageAsync makes to one snapshot, so a
        // concurrent insert/delete between them can't produce a TotalCount inconsistent with the
        // returned page. Owned here (Service via IUnitOfWork), not inside the repository.
        await unitOfWork.BeginAsync(ct, IsolationLevel.RepeatableRead);
        try
        {
            var (items, totalCount) = await boardRepository.QueryPageAsync(query, ct);
            await unitOfWork.CommitAsync(ct);
            return (items.Select(BoardMapper.ToResponse), totalCount);
        }
        catch
        {
            await unitOfWork.RollbackAsync(ct);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BoardResponse>> GetNoticedBoardsAsync(string type, CancellationToken ct = default)
        => (await boardRepository.GetNoticedAsync(type, ct)).Select(BoardMapper.ToResponse);

    /// <inheritdoc />
    public async Task<BoardResponse?> GetBoardByIdAsync(long id, string? requiredType = null, CancellationToken ct = default)
    {
        Board? board = await boardRepository.FindActiveByIdAsync(id, ct);
        if (board == null || (requiredType != null && board.Type != requiredType))
            return null;

        return BoardMapper.ToResponse(board);
    }

    /// <inheritdoc />
    public bool CanView(BoardResponse board, AccountResponse? viewer) =>
        Board.CanBeViewedBy(
            board.Type, board.Deleted, board.Locked, board.Writer,
            viewer?.Nickname, viewer?.Role == Role.Admin);

    /// <inheritdoc />
    public async Task<BoardAttachedFileDto?> GetAttachedFileByBoardIdAsync(long boardId, CancellationToken ct = default)
    {
        BoardAttachedFile? file = await boardAttachedFileRepository.FindByBoardIdAsync(boardId, ct);
        return file == null ? null : BoardAttachedFileMapper.ToResponse(file);
    }

    /// <inheritdoc />
    public async Task<Dictionary<long, BoardAttachedFileDto?>> GetAttachedFilesForBoardsAsync(IEnumerable<long> boardIds, CancellationToken ct = default)
    {
        Dictionary<long, BoardAttachedFile?> domain = await boardAttachedFileRepository.GetByBoardIdsAsync(boardIds, ct);
        return domain.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value == null ? null : (BoardAttachedFileDto?)BoardAttachedFileMapper.ToResponse(kvp.Value));
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BoardCommentResponse>> GetCommentsByBoardIdAsync(long boardId, CancellationToken ct = default)
        => (await boardCommentRepository.GetByBoardIdOrderedAsync(boardId, ct)).Select(BoardCommentMapper.ToResponse);

    /// <inheritdoc />
    public async Task<Dictionary<long, int>> GetCommentCountsForBoardsAsync(IEnumerable<long> boardIds, CancellationToken ct = default)
        => await boardCommentRepository.GetCommentCountsByBoardIdsAsync(boardIds, ct);

    /// <inheritdoc />
    public async Task<ServiceResult> WriteBoardAsync(BoardRequest request, bool isAdmin, AttachedFileDto? attachedFile, CancellationToken ct = default)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var fileError = attachmentContent.ValidateAttachedFile(attachedFile);
        if (fileError != null) return fileError;

        string content = attachmentContent.SanitizeAndDecryptContent(request.Content ?? "");

        await unitOfWork.BeginAsync(ct);
        try
        {
            var boardResult = Board.Create(request.Type, request.Title, content, request.Writer, utcNow);
            if (boardResult.IsError)
            {
                await unitOfWork.RollbackAsync(ct);
                return ServiceResult.FromError(boardResult.FirstError);
            }

            var board = boardResult.Value;
            if (request.Locked) board.Lock(utcNow);
            // Pinning a post site-wide is an admin-only action — enforced here, not just by the
            // caller, so a future write path can't accidentally honor a non-admin's Noticed=true.
            if (request.Noticed && isAdmin) board.Pin(utcNow);

            long boardId = await boardRepository.WriteBoardAsync(board, ct);

            if (attachedFile != null)
            {
                // Build a unique storage path and validate the attachment record BEFORE uploading, so a
                // record the domain would reject (e.g. an over-long file name) never leaves an orphaned
                // file in storage; then upload, then persist the record.
                string filePath = BuildAttachedFilePath(request.Type!, boardId, attachedFile.FileName);
                var fileResult = BoardAttachedFile.Create(boardId, attachedFile.Size,
                    Path.GetFileNameWithoutExtension(attachedFile.FileName),
                    Path.GetExtension(attachedFile.FileName),
                    filePath.Replace('\\', '/')); // Normalize path separators for URL use.
                if (fileResult.IsError)
                {
                    await unitOfWork.RollbackAsync(ct);
                    return ServiceResult.FromError(fileResult.FirstError);
                }

                bool uploaded = await fileClient.UploadAsync(attachedFile.Bytes, attachedFile.ContentType, filePath, settings.Value.FileStorageBaseUrl ?? "", ct);
                if (!uploaded)
                {
                    await unitOfWork.RollbackAsync(ct);
                    return ServiceResult.Failure("Board.AttachmentUploadFailed", "Failed to upload the attached file.");
                }

                await boardAttachedFileRepository.CreateAsync(fileResult.Value, ct);
            }

            await unitOfWork.CommitAsync(ct);
            return ServiceResult.Ok();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to write board post (Type={Type})", request.Type);
            await unitOfWork.RollbackAsync(ct);
            return ServiceResult.Failure("Board.WriteFailed", "Input is invalid");
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> EditBoardAsync(BoardRequest request, string writerNickname, bool isAdmin, AttachedFileDto? newFile, CancellationToken ct = default)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var fileError = attachmentContent.ValidateAttachedFile(newFile);
        if (fileError != null) return fileError;

        string content = attachmentContent.SanitizeAndDecryptContent(request.Content ?? "");

        await unitOfWork.BeginAsync(ct);
        try
        {
            // Row-lock the post for the rest of the transaction. The subsequent UpdateEntityAsync
            // rewrites every column from this snapshot, so without the lock a concurrent
            // IncrementViewAsync (or an admin Noticed/Locked toggle) committing between this read
            // and the commit would be silently overwritten — the same full-row-overwrite race the
            // Account column-scoped writes were introduced to fix.
            Board? board = await boardRepository.FindActiveByIdForUpdateAsync(request.Id, ct);

            // A caller always states the type it expects to edit (see WriteCommentAsync's
            // requiredType for the same pattern); without this check a post owned by the same
            // user under one board type could be edited through another type's endpoint.
            if (board == null || board.Deleted || (request.Type != null && board.Type != request.Type))
            {
                await unitOfWork.RollbackAsync(ct);
                return ServiceResult.NotFound("Board.NotFound", "Input is invalid");
            }

            // Only the original writer may edit content. An admin who is not the writer may still
            // reach this point, but only to clear an existing lock (see below) -- everything else
            // is rejected. This is the only way an admin can ever clear a lock on someone else's
            // post, since the ownership gate below would otherwise block them from this method
            // entirely. (The Forum edit page itself is owner-only, so this path is reached by
            // posting to EditFreeBoard directly rather than through that page.)
            bool isOwner = board.CanBeEditedBy(writerNickname);
            bool adminClearingSomeoneElsesLock = !request.Locked && board.CanLockBeClearedBy(writerNickname, isAdmin);
            switch (isOwner)
            {
                case false when !adminClearingSomeoneElsesLock:
                    await unitOfWork.RollbackAsync(ct);
                    logger.LogWarning("Board edit refused: {Requester} may not edit post {BoardId}", writerNickname, board.Id);
                    return ServiceResult.Fail("Input is invalid");
                case true:
                {
                    var updateResult = board.Update(request.Title, content, utcNow);
                    if (updateResult.IsError)
                    {
                        await unitOfWork.RollbackAsync(ct);
                        return ServiceResult.FromError(updateResult.FirstError);
                    }

                    // Apply the lock from the request. A lock is only ever raised by the post's own
                    // author (at write or edit time) -- an admin who is not the author can only CLEAR
                    // one (see the default branch below), never impose it -- so the author may lower
                    // it as well as raise it. (Noticed, by contrast, is set only at write time and
                    // deliberately kept out of this edit path.)
                    if (request.Locked) board.Lock(utcNow);
                    else board.Unlock(utcNow);
                    break;
                }
                default:
                    // adminClearingSomeoneElsesLock: an admin acting on a post they don't own may only
                    // clear its lock -- title and content are left untouched.
                    board.Unlock(utcNow);
                    logger.LogInformation("Lock on post {BoardId} of {Writer} cleared by admin {Requester}", board.Id, board.Writer, writerNickname);
                    break;
            }

            await boardRepository.UpdateEntityAsync(board, ct);

            BoardAttachedFile? previous = await boardAttachedFileRepository.FindByBoardIdAsync(request.Id, ct);
            ServiceResult attachmentResult = await HandleAttachmentAsync(board.Type, board.Id, previous, newFile, ct);
            if (!attachmentResult.Success)
            {
                await unitOfWork.RollbackAsync(ct);
                return attachmentResult;
            }

            await unitOfWork.CommitAsync(ct);
            return ServiceResult.Ok();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to edit board post {BoardId}", request.Id);
            await unitOfWork.RollbackAsync(ct);
            return ServiceResult.Failure("Board.EditFailed", "Input is invalid");
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DeleteBoardAsync(long boardId, string boardType, string requesterNickname, bool isAdmin, CancellationToken ct = default)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        await unitOfWork.BeginAsync(ct);
        try
        {
            // Row-lock the post: SoftDelete goes out through the full-row UpdateEntityAsync, so a
            // concurrent IncrementViewAsync / admin toggle committing between the read and the
            // commit would otherwise be lost to this stale snapshot.
            Board? board = await boardRepository.FindActiveByIdForUpdateAsync(boardId, ct);

            if (board == null || board.Deleted || board.Type != boardType)
            {
                await unitOfWork.RollbackAsync(ct);
                return ServiceResult.NotFound("Board.NotFound", "Input is invalid");
            }

            // Admins may delete any post; non-admins may only delete their own.
            if (!board.CanBeDeletedBy(requesterNickname, isAdmin))
            {
                await unitOfWork.RollbackAsync(ct);
                logger.LogWarning("Board delete refused: {Requester} may not delete post {BoardId}", requesterNickname, boardId);
                return ServiceResult.Fail("You do not have permission to delete.");
            }

            // Cannot fail: the post was loaded as active and not deleted just above (SoftDelete only refuses an already-deleted post).
            _ = board.SoftDelete(utcNow);

            await boardRepository.UpdateEntityAsync(board, ct);
            await unitOfWork.CommitAsync(ct);
            logger.LogInformation("Board post {BoardId} of {Writer} deleted by {Requester} (admin: {IsAdmin})", boardId, board.Writer, requesterNickname, isAdmin);
            return ServiceResult.Ok();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to delete board post {BoardId}", boardId);
            await unitOfWork.RollbackAsync(ct);
            return ServiceResult.Failure("Board.DeleteFailed", "Input is invalid");
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> WriteCommentAsync(BoardCommentRequest request, string? requiredOwnerNickname = null, bool isAdmin = false, string? requiredType = null, CancellationToken ct = default)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        string content = attachmentContent.SanitizeContent(request.Content ?? "");

        await unitOfWork.BeginAsync(ct);
        try
        {
            // Row-locks the post for the rest of this transaction, so concurrent commenters on
            // the same post serialize instead of both reading the same Count() and racing to
            // insert duplicate Order values.
            Board? board = await boardRepository.FindActiveByIdForUpdateAsync(request.BoardId, ct);
            if (board == null || (requiredType != null && board.Type != requiredType))
            {
                await unitOfWork.RollbackAsync(ct);
                return ServiceResult.NotFound("Board.NotFound", "Input is invalid");
            }

            // When requiredOwnerNickname is set, restrict commenting to the post owner only.
            if (requiredOwnerNickname != null && !board.IsOwnedBy(requiredOwnerNickname))
            {
                await unitOfWork.RollbackAsync(ct);
                return ServiceResult.Fail("You do not have permission to write a comment.");
            }

            IEnumerable<BoardComment> existing = await boardCommentRepository.GetByBoardIdOrderedAsync(request.BoardId, ct);
            List<BoardComment> existingList = existing.ToList();

            // The comment order continues from the highest existing order, so that a prior
            // soft-deleted comment (excluded from existingList) cannot cause a new comment to
            // collide with the Order of one still active.
            long order = existingList.Count == 0 ? 0 : existingList.Max(c => c.Order) + 1;

            var commentResult = BoardComment.Create(request.BoardId, order, request.AvatarImagePath, request.Writer, content, utcNow);
            if (commentResult.IsError)
            {
                await unitOfWork.RollbackAsync(ct);
                return ServiceResult.FromError(commentResult.FirstError);
            }

            var addResult = board.AddComment(commentResult.Value, utcNow, request.Writer, isAdmin);
            if (addResult.IsError)
            {
                await unitOfWork.RollbackAsync(ct);
                return ServiceResult.FromError(addResult.FirstError);
            }

            await boardCommentRepository.CreateAsync(commentResult.Value, ct);
            await unitOfWork.CommitAsync(ct);
            return ServiceResult.Ok();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to write comment on board {BoardId}", request.BoardId);
            await unitOfWork.RollbackAsync(ct);
            return ServiceResult.Failure("Board.WriteCommentFailed", "Input is invalid");
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> IncrementBoardViewAsync(long boardId, CancellationToken ct = default)
    {
        try
        {
            await boardRepository.IncrementViewAsync(boardId, ct);
            return ServiceResult.Ok();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to increment view count for board {BoardId}", boardId);
            return ServiceResult.Failure("Board.IncrementViewFailed", "Input is invalid");
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DeleteCommentAsync(long commentId, string requesterNickname, bool isAdmin, string? requiredType = null, CancellationToken ct = default)
    {
        await unitOfWork.BeginAsync(ct);
        try
        {
            // Lock the row for the rest of this transaction, consistent with every other write in
            // this file (WriteCommentAsync/EditBoardAsync/DeleteBoardAsync): without it, two
            // concurrent deletes of the same comment could both read Deleted=false, both succeed
            // in memory, and both issue a redundant write.
            BoardComment? comment = await boardCommentRepository.FindByIdForUpdateAsync(commentId, ct);
            if (comment == null)
            {
                await unitOfWork.RollbackAsync(ct);
                return ServiceResult.NotFound("BoardComment.NotFound", "Input is invalid");
            }

            bool isPrivateNoteComment = false;
            if (requiredType != null)
            {
                // The comment's parent post must belong to the caller's board type — otherwise a
                // caller scoped to one board type could delete a comment on a post of a different
                // type just by supplying its comment ID.
                Board? parent = await boardRepository.FindActiveByIdAsync(comment.BoardId, ct);
                if (parent == null || parent.Type != requiredType)
                {
                    await unitOfWork.RollbackAsync(ct);
                    return ServiceResult.NotFound("BoardComment.NotFound", "Input is invalid");
                }
                isPrivateNoteComment = parent.IsPrivateNote;
            }

            // Admins may delete any comment, except on a private note (owner-only, no admin
            // bypass — matches Board.CanBeDeletedBy's PrivateNote rule).
            bool effectiveIsAdmin = isAdmin && !isPrivateNoteComment;
            if (!comment.CanBeDeletedBy(requesterNickname, effectiveIsAdmin))
            {
                await unitOfWork.RollbackAsync(ct);
                logger.LogWarning("Comment delete refused: {Requester} may not delete comment {CommentId}", requesterNickname, commentId);
                return ServiceResult.Fail("You do not have permission to delete.");
            }

            var deleteResult = comment.SoftDelete();
            if (deleteResult.IsError)
            {
                await unitOfWork.RollbackAsync(ct);
                return ServiceResult.FromError(deleteResult.FirstError);
            }

            await boardCommentRepository.UpdateEntityAsync(comment, ct);
            await unitOfWork.CommitAsync(ct);
            logger.LogInformation("Comment {CommentId} of {Writer} deleted by {Requester} (admin: {IsAdmin})", commentId, comment.Writer, requesterNickname, isAdmin);
            return ServiceResult.Ok();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to delete comment {CommentId}", commentId);
            await unitOfWork.RollbackAsync(ct);
            return ServiceResult.Failure("Board.DeleteCommentFailed", "Input is invalid");
        }
    }

    /// <summary>
    /// Handles the four possible attachment states when editing a board post:
    /// replace (previous + new), clear (previous only), add (new only), or no-op (neither).
    /// Returns a failed <see cref="ServiceResult"/> when a required upload or validation fails,
    /// so the caller can roll back the transaction instead of persisting an attachment record
    /// for a file that was never saved or is otherwise invalid.
    /// </summary>
    private async Task<ServiceResult> HandleAttachmentAsync(string boardType, long boardId, BoardAttachedFile? previous, AttachedFileDto? newFile, CancellationToken ct = default)
    {
        if (previous != null && newFile != null)
        {
            // Replace: upload the new file and overwrite the existing attachment record.
            string filePath = BuildAttachedFilePath(boardType, boardId, newFile.FileName);
            var fileResult = BoardAttachedFile.Create(previous.BoardId, newFile.Size,
                Path.GetFileNameWithoutExtension(newFile.FileName),
                Path.GetExtension(newFile.FileName),
                filePath.Replace('\\', '/'));
            if (fileResult.IsError) return ServiceResult.FromError(fileResult.FirstError);

            // Validated first, uploaded second: a rejected record must not leave an orphaned file.
            bool uploaded = await fileClient.UploadAsync(newFile.Bytes, newFile.ContentType, filePath, settings.Value.FileStorageBaseUrl ?? "", ct);
            if (!uploaded) return ServiceResult.Failure("Board.AttachmentUploadFailed", "Failed to upload the attached file.");

            await boardAttachedFileRepository.UpdateEntityAsync(
                BoardAttachedFile.Reconstitute(previous.Id, fileResult.Value.BoardId, fileResult.Value.Size,
                    fileResult.Value.Name, fileResult.Value.Extension, fileResult.Value.Path), ct);
        }
        else if (previous != null && newFile == null)
        {
            // Clear: zero-out the attachment record without uploading anything. Name/Path are
            // intentionally empty here as an empty-attachment sentinel, not user-submitted data,
            // so Reconstitute (not Create, which would reject an empty name/path) is correct.
            await boardAttachedFileRepository.UpdateEntityAsync(
                BoardAttachedFile.Reconstitute(previous.Id, previous.BoardId, 0, "", "", ""), ct);
        }
        else if (previous == null && newFile != null)
        {
            // Add: upload and create a new attachment record for a post that had none before.
            string filePath = BuildAttachedFilePath(boardType, boardId, newFile.FileName);
            var fileResult = BoardAttachedFile.Create(boardId, newFile.Size,
                Path.GetFileNameWithoutExtension(newFile.FileName),
                Path.GetExtension(newFile.FileName),
                filePath.Replace('\\', '/'));
            if (fileResult.IsError) return ServiceResult.FromError(fileResult.FirstError);

            // Validated first, uploaded second: a rejected record must not leave an orphaned file.
            bool uploaded = await fileClient.UploadAsync(newFile.Bytes, newFile.ContentType, filePath, settings.Value.FileStorageBaseUrl ?? "", ct);
            if (!uploaded) return ServiceResult.Failure("Board.AttachmentUploadFailed", "Failed to upload the attached file.");

            await boardAttachedFileRepository.CreateAsync(fileResult.Value, ct);
        }
        // previous == null && newFile == null: nothing to do.

        return ServiceResult.Ok();
    }

    /// <inheritdoc />
    public Task<SummernoteUploadResult> UploadSummernoteImageAsync(AttachedFileDto file, string area, string boardType, CancellationToken ct = default)
        => attachmentContent.UploadSummernoteImageAsync(file, area, boardType, ct);

    /// <inheritdoc />
    public Task<byte[]?> DownloadFileAsync(string filePath, CancellationToken ct = default)
        => attachmentContent.DownloadFileAsync(filePath, ct);

    /// <summary>
    /// Builds the remote storage path for a board attachment
    /// (<c>upload/{area}/{boardType}/boardAttachedFiles/{boardId}/{GUID}{ext}</c>). The original file
    /// name is replaced by an upper-case GUID (only its extension is kept), so names cannot collide or
    /// traverse paths; the original name is kept on the attachment record instead.
    /// </summary>
    private static string BuildAttachedFilePath(string boardType, long boardId, string fileName)
    {
        // Map the board type to the correct storage area; default to "Forum" for unknown types.
        string area = _boardAreaMap.GetValueOrDefault(boardType, "Forum");
        string guid = Guid.NewGuid().ToString().ToUpper();
        string ext = Path.GetExtension(fileName);
        // A storage key, not a local path: always "/"-separated, whatever OS this runs on.
        return $"upload/{area}/{boardType}/boardAttachedFiles/{boardId}/{guid}{ext}";
    }

    /// <inheritdoc />
    public Task<(string Html, bool HasImages)> PrepareHtmlForDisplayAsync(string html, CancellationToken ct = default)
        => attachmentContent.PrepareHtmlForDisplayAsync(html, ct);
}
