using Maroik.Core.Contract.Dtos;

namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Service interface for board (forum / private note) business logic,
/// including post lifecycle, comments, file attachments, and Summernote image uploads.
/// </summary>
public interface IBoardService
{
    /// <summary>
    /// Returns one page of posts matching <paramref name="query"/>, plus the total matching count
    /// (for pagination), executed as a single SQL query rather than materializing the whole
    /// board-type bucket into memory first.
    /// </summary>
    Task<(IEnumerable<BoardResponse> Items, int TotalCount)> GetBoardPageAsync(BoardPageQuery query, CancellationToken ct = default);

    /// <summary>Returns all non-deleted, pinned ("Noticed") posts of the specified board type, newest first.</summary>
    Task<IEnumerable<BoardResponse>> GetNoticedBoardsAsync(string type, CancellationToken ct = default);

    /// <summary>
    /// Returns a single post by ID. When <paramref name="requiredType"/> is set, the post must be
    /// of that board type — so a caller scoped to one board type (e.g. the public Forum endpoint)
    /// cannot read a post of a different type (e.g. another account's PrivateNote) just by
    /// supplying its ID.
    /// </summary>
    Task<BoardResponse?> GetBoardByIdAsync(long id, string? requiredType = null, CancellationToken ct = default);

    /// <summary>
    /// True when <paramref name="viewer"/> may view <paramref name="board"/>. Thin projection-based
    /// wrapper over the authoritative <c>Board.CanBeViewedBy</c> domain rule: private notes are
    /// visible only to their author; other board types are visible to anyone unless locked, in
    /// which case only the author or an Admin may view them; a deleted post is never viewable.
    /// </summary>
    bool CanView(BoardResponse board, AccountResponse? viewer);

    /// <summary>Returns the attached file metadata for the given board post, or null if none.</summary>
    Task<BoardAttachedFileDto?> GetAttachedFileByBoardIdAsync(long boardId, CancellationToken ct = default);

    /// <summary>
    /// Batch-loads attached file metadata for multiple board IDs in a single query.
    /// Returns a dictionary keyed by board ID; value is null when a post has no attachment.
    /// </summary>
    Task<Dictionary<long, BoardAttachedFileDto?>> GetAttachedFilesForBoardsAsync(IEnumerable<long> boardIds, CancellationToken ct = default);

    /// <summary>Returns all non-deleted comments for the given board post.</summary>
    Task<IEnumerable<BoardCommentResponse>> GetCommentsByBoardIdAsync(long boardId, CancellationToken ct = default);

    /// <summary>
    /// Batch-loads comment counts for multiple board IDs.
    /// Used to render count badges on the board list without loading comment bodies.
    /// </summary>
    Task<Dictionary<long, int>> GetCommentCountsForBoardsAsync(IEnumerable<long> boardIds, CancellationToken ct = default);

    /// <summary>
    /// Creates a new board post and optionally saves an attached file. <paramref name="isAdmin"/>
    /// gates <see cref="BoardRequest.Noticed"/> — a non-admin's request can never pin a post, no
    /// matter what the caller populated the DTO with.
    /// </summary>
    Task<ServiceResult> WriteBoardAsync(BoardRequest request, bool isAdmin, AttachedFileDto? attachedFile, CancellationToken ct = default);

    /// <summary>
    /// Edits an existing post. Only the original author (matched by <paramref name="writerNickname"/>) may edit
    /// its title and content, and may set or clear its lock via <see cref="BoardRequest.Locked"/>. An
    /// <paramref name="isAdmin"/> requester who is not the author may only clear an existing lock (title
    /// and content stay untouched). Replaces the attached file if <paramref name="newFile"/> is provided and
    /// clears it when it is not.
    /// </summary>
    Task<ServiceResult> EditBoardAsync(BoardRequest request, string writerNickname, bool isAdmin, AttachedFileDto? newFile, CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes a board post. Admins can delete any post; regular users can only delete their own.
    /// <paramref name="boardType"/> must match the post's actual type, so a caller scoped to one
    /// board type (e.g. the public Forum endpoint) cannot target a post of a different type
    /// (e.g. another account's PrivateNote) just by supplying its ID.
    /// </summary>
    Task<ServiceResult> DeleteBoardAsync(long boardId, string boardType, string requesterNickname, bool isAdmin, CancellationToken ct = default);

    /// <summary>
    /// Adds a comment to a board post.
    /// When <paramref name="requiredOwnerNickname"/> is set, only that user may comment (used for private notes).
    /// When <paramref name="requiredType"/> is set, the target post must be of that board type — so a
    /// caller scoped to one board type (e.g. the public Forum endpoint) cannot comment on a post of a
    /// different type (e.g. another account's PrivateNote) just by supplying its ID.
    /// A locked post still accepts comments from its owner (<paramref name="request"/>.Writer) or an admin.
    /// </summary>
    Task<ServiceResult> WriteCommentAsync(BoardCommentRequest request, string? requiredOwnerNickname = null, bool isAdmin = false, string? requiredType = null, CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes a comment. Admins can delete any comment; regular users can only delete their own.
    /// When <paramref name="requiredType"/> is set, the comment's parent post must be of that board
    /// type — so a caller scoped to one board type cannot delete a comment belonging to a post of a
    /// different type just by supplying its comment ID.
    /// </summary>
    Task<ServiceResult> DeleteCommentAsync(long commentId, string requesterNickname, bool isAdmin, string? requiredType = null, CancellationToken ct = default);

    /// <summary>Increments the view counter of a board post by 1.</summary>
    Task<ServiceResult> IncrementBoardViewAsync(long boardId, CancellationToken ct = default);

    /// <summary>
    /// Validates and stores an image uploaded from the Summernote editor.
    /// Returns the stored file data so the editor can render an inline preview. <paramref name="actorEmail"/> is the
    /// signed-in account uploading it, recorded when the image is refused.
    /// </summary>
    Task<SummernoteUploadResult> UploadSummernoteImageAsync(AttachedFileDto file, string area, string boardType, string actorEmail, CancellationToken ct = default);

    /// <summary>
    /// Opens the attachment of post <paramref name="boardId"/> for <paramref name="viewer"/> (null for
    /// an anonymous visitor). The post must exist, be of <paramref name="boardType"/> and be visible to
    /// the viewer under the same rule as the post itself; otherwise nothing is opened and the result
    /// says why. On success the file is streamed from file storage.
    /// </summary>
    Task<(ServiceResult Result, AttachmentDownload? File)> OpenAttachedFileAsync(
        long boardId, string boardType, AccountResponse? viewer, CancellationToken ct = default);

    /// <summary>
    /// Prepares HTML content for display by downloading each inline Summernote image,
    /// embedding it as Base64 in <c>data-file</c> / <c>data-contenttype</c> attributes,
    /// and replacing the stored file path in <c>alt</c> with its RSA-encrypted form (an image that
    /// cannot be downloaded is removed). Returns the transformed HTML and a flag indicating whether
    /// any images were present.
    /// </summary>
    Task<(string Html, bool HasImages)> PrepareHtmlForDisplayAsync(string html, CancellationToken ct = default);
}
