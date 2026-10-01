using ErrorOr;
using Maroik.Core.Domain.Localization;
using Maroik.Core.Domain.Primitives;

namespace Maroik.Core.Domain.Board;

/// <summary>
/// Aggregate root for a board post (FreeForum or PrivateNote).
/// Comments and attached files are persisted independently via their own repositories
/// (<see cref="BoardComment"/>, <see cref="BoardAttachedFile"/>) rather than hydrated as part of
/// this aggregate, so this class does not mediate their persistence beyond the validation
/// <see cref="AddComment"/> performs at write time.
/// </summary>
public sealed class Board : AggregateRoot<long>
{
    /// <summary>Backing list for <see cref="Comments"/>: only the comments added in this call, never the stored ones.</summary>
    private readonly List<BoardComment> _comments = [];

    /// <summary>Board type identifier (e.g. "FreeForum", "PrivateNote").</summary>
    public string Type { get; }

    /// <summary>Post title.</summary>
    public string Title { get; private set; }

    /// <summary>Post body content in Summernote HTML format.</summary>
    public string? Content { get; private set; }

    /// <summary>Nickname of the author.</summary>
    public string Writer { get; }

    /// <summary>UTC timestamp when the post was created.</summary>
    public DateTime Created { get; private set; }

    /// <summary>UTC timestamp of the most recent edit.</summary>
    public DateTime Updated { get; private set; }

    /// <summary>View count (incremented each time the post is opened).</summary>
    public long View { get; private set; }

    /// <summary>Soft-delete flag.</summary>
    public bool Deleted { get; private set; }

    /// <summary>
    /// When true, only the author and admins may view the post or add comments to it
    /// (see <see cref="CanBeViewedBy(string?, bool)"/> / <see cref="AddComment"/>).
    /// </summary>
    public bool Locked { get; private set; }

    /// <summary>When true, the post is pinned at the top of the list as a notice.</summary>
    public bool Noticed { get; private set; }

    /// <summary>
    /// Comments added to this post via <see cref="AddComment"/> during the current call, exposed for
    /// inspection immediately after adding one. Not hydrated from the repository on a normal load
    /// (comments are fetched separately, e.g. via <c>IBoardCommentRepository</c>), so this is empty
    /// on a freshly-loaded post and must not be treated as the full comment list.
    /// </summary>
    public IReadOnlyList<BoardComment> Comments => _comments.AsReadOnly();

    /// <summary>"New post" constructor: stamps <see cref="Created"/>/<see cref="Updated"/>. Used by <see cref="Create"/> only.</summary>
    private Board(
        long id,
        string type,
        string title,
        string? content,
        string writer,
        DateTime utcNow) : base(id)
    {
        Type = type;
        Title = title;
        Content = content;
        Writer = writer;
        Created = utcNow;
        Updated = utcNow;
    }

    /// <summary>Reconstitution constructor: assigns every field verbatim from trusted storage with no new timestamps.</summary>
    private Board(
        long id, string type, string title, string? content, string writer,
        DateTime created, DateTime updated, long view, bool deleted, bool locked, bool noticed) : base(id)
    {
        Type = type;
        Title = title;
        Content = content;
        Writer = writer;
        Created = created;
        Updated = updated;
        View = view;
        Deleted = deleted;
        Locked = locked;
        Noticed = noticed;
    }

    // ------------------------------------------------------------------------
    // Factory / Reconstitution
    // ------------------------------------------------------------------------

    /// <summary>
    /// Rebuilds a <see cref="Board"/> from trusted raw values from the repository layer.
    /// Bypasses validation — use only with data from the trusted repository layer.
    /// Child comments and attached files must be loaded separately.
    /// </summary>
    public static Board Reconstitute(
        long id,
        string type,
        string title,
        string? content,
        string writer,
        DateTime created,
        DateTime updated,
        long view,
        bool deleted,
        bool locked,
        bool noticed)
    {
        return new Board(id, type, title, content, writer, created, updated, view, deleted, locked, noticed);
    }

    /// <summary>
    /// Creates a new board post.
    /// </summary>
    public static ErrorOr<Board> Create(
        string? type,
        string? title,
        string? content,
        string? writer,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(type))
            return LocalizableError.Validation("Board.TypeEmpty", "Board type cannot be empty.");

        // Constrain to the known taxonomy here rather than trusting the caller: BuildAttachedFilePath
        // in the service layer interpolates this value into a file-storage path, and otherwise the DB
        // Board_Type_check constraint would be the only thing rejecting anything else.
        if (!BoardTypes.IsKnown(type))
            return LocalizableError.Validation("Board.TypeInvalid", "Board type is not a recognised value.");

        if (string.IsNullOrWhiteSpace(title))
            return LocalizableError.Validation("Board.TitleEmpty", "Post title cannot be empty.");

        if (title.Length > TitledContentPolicy.MaxTitleLength)
            return LocalizableError.Validation("Board.TitleTooLong", "Post title must be {0} characters or fewer.", TitledContentPolicy.MaxTitleLength);

        if (content?.Length > TitledContentPolicy.MaxBodyLength)
            return LocalizableError.Validation("Board.ContentTooLong", "Post content must be {0} characters or fewer.", TitledContentPolicy.MaxBodyLength);

        if (string.IsNullOrWhiteSpace(writer))
            return LocalizableError.Validation("Board.WriterEmpty", "Post author (writer) cannot be empty.");

        var board = new Board(0, type, title, content, writer, utcNow);
        return board;
    }

    // ------------------------------------------------------------------------
    // Domain behaviours
    // ------------------------------------------------------------------------

    /// <summary>Updates the post title and content.</summary>
    public ErrorOr<Success> Update(string? title, string? content, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(title))
            return LocalizableError.Validation("Board.TitleEmpty", "Post title cannot be empty.");

        if (title.Length > TitledContentPolicy.MaxTitleLength)
            return LocalizableError.Validation("Board.TitleTooLong", "Post title must be {0} characters or fewer.", TitledContentPolicy.MaxTitleLength);

        if (content?.Length > TitledContentPolicy.MaxBodyLength)
            return LocalizableError.Validation("Board.ContentTooLong", "Post content must be {0} characters or fewer.", TitledContentPolicy.MaxBodyLength);

        if (Deleted)
            return LocalizableError.Conflict("Board.Deleted", "Cannot update a deleted post.");

        Title = title;
        Content = content;
        Updated = utcNow;
        return Result.Success;
    }

    /// <summary>Increments the view counter by one.</summary>
    public void IncrementView()
    {
        View++;
    }

    /// <summary>
    /// Adds a comment to this post. A locked post rejects new comments from anyone except its
    /// owner or an admin, mirroring who is still allowed to view a locked post at all.
    /// </summary>
    public ErrorOr<Success> AddComment(BoardComment comment, DateTime utcNow, string? commenterNickname = null, bool isAdmin = false)
    {
        if (Locked && !((commenterNickname != null && IsOwnedBy(commenterNickname)) || isAdmin))
            return LocalizableError.Conflict("Board.Locked", "Cannot add a comment to a locked post.");

        if (Deleted)
            return LocalizableError.Conflict("Board.Deleted", "Cannot add a comment to a deleted post.");

        if (comment.BoardId != Id)
            return LocalizableError.Validation("Board.CommentMismatch", "Comment does not belong to this post.");

        _comments.Add(comment);
        Updated = utcNow;
        return Result.Success;
    }

    /// <summary>Locks the post: only its author and admins can still view or comment on it.</summary>
    public void Lock(DateTime utcNow)
    {
        Locked = true;
        Updated = utcNow;
    }

    /// <summary>Unlocks the post, making it viewable and commentable by everyone again.</summary>
    public void Unlock(DateTime utcNow)
    {
        Locked = false;
        Updated = utcNow;
    }

    /// <summary>Pins the post at the top of the list as a notice.</summary>
    public void Pin(DateTime utcNow)
    {
        Noticed = true;
        Updated = utcNow;
    }

    /// <summary>Unpins the post from the notice position.</summary>
    public void Unpin(DateTime utcNow)
    {
        Noticed = false;
        Updated = utcNow;
    }

    /// <summary>Marks the post as soft-deleted.</summary>
    public ErrorOr<Success> SoftDelete(DateTime utcNow)
    {
        if (Deleted)
            return LocalizableError.Conflict("Board.AlreadyDeleted", "Post is already deleted.");

        Deleted = true;
        Updated = utcNow;
        return Result.Success;
    }

    /// <summary>True when this post is a private note (visible to its author only).</summary>
    public bool IsPrivateNote => string.Equals(Type, BoardTypes.PrivateNote, StringComparison.Ordinal);

    /// <summary>Returns true when <paramref name="nickname"/> is this post's author.</summary>
    public bool IsOwnedBy(string nickname) => IsOwnedBy(Writer, nickname);

    /// <summary>
    /// Field-level ownership comparison shared by <see cref="Board"/> and <see cref="BoardComment"/>
    /// (both use the identical "author nickname equals viewer nickname" rule), and by
    /// <c>Maroik.Core.Contract.Misc.Extensions.AccountResponseExtensions.IsOwner</c> for the DTO-side
    /// UI-affordance mirror of the same rule — kept in one place so all three can't drift apart.
    /// </summary>
    public static bool IsOwnedBy(string? writer, string? viewerNickname) =>
        !string.IsNullOrEmpty(writer) && string.Equals(writer, viewerNickname, StringComparison.Ordinal);

    /// <summary>
    /// Returns true when a viewer identified by <paramref name="viewerNickname"/> (<see langword="null"/>
    /// = anonymous) with <paramref name="viewerIsAdmin"/> may open this post. A private note is visible
    /// only to its author (no admin bypass); any other board type is public unless locked, and a locked
    /// post is visible only to its author or an admin. A soft-deleted post is never viewable.
    /// </summary>
    public bool CanBeViewedBy(string? viewerNickname, bool viewerIsAdmin) =>
        CanBeViewedBy(Type, Deleted, Locked, Writer, viewerNickname, viewerIsAdmin);

    /// <summary>
    /// Field-level overload of <see cref="CanBeViewedBy(string?, bool)"/> for callers that only hold a
    /// projection of a post (e.g. a service working from a read DTO) rather than a full aggregate.
    /// </summary>
    public static bool CanBeViewedBy(
        string? type, bool deleted, bool locked, string? writer,
        string? viewerNickname, bool viewerIsAdmin)
    {
        if (deleted)
            return false;

        bool isOwner = viewerNickname != null && string.Equals(writer, viewerNickname, StringComparison.Ordinal);

        return string.Equals(type, BoardTypes.PrivateNote, StringComparison.Ordinal)
            ? isOwner
            : !locked || isOwner || viewerIsAdmin;
    }

    /// <summary>Returns true when <paramref name="nickname"/> may edit this post (owner only, no admin bypass).</summary>
    public bool CanBeEditedBy(string nickname) => IsOwnedBy(nickname);

    /// <summary>
    /// Returns true when <paramref name="nickname"/> may delete this post. A private note is
    /// deletable only by its author (no admin bypass, matching <see cref="CanBeViewedBy(string?, bool)"/>);
    /// any other board type is deletable by its owner or any admin.
    /// </summary>
    public bool CanBeDeletedBy(string nickname, bool isAdmin) =>
        IsPrivateNote ? IsOwnedBy(nickname) : IsOwnedBy(nickname) || isAdmin;

    /// <summary>
    /// Returns true when <paramref name="nickname"/> is an admin who is <em>not</em> this post's author
    /// and may nonetheless clear its lock — the one moderation action an admin may take on someone
    /// else's post (title and content stay author-only, see <see cref="CanBeEditedBy"/>). A private
    /// note is single-owner (no admin bypass, matching <see cref="CanBeViewedBy(string?, bool)"/> and
    /// <see cref="CanBeDeletedBy"/>), and there is nothing to clear on an unlocked post.
    /// </summary>
    public bool CanLockBeClearedBy(string nickname, bool isAdmin) =>
        isAdmin && Locked && !IsPrivateNote && !IsOwnedBy(nickname);
}
