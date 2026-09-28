using ErrorOr;
using Maroik.Core.Domain.Localization;
using Maroik.Core.Domain.Primitives;

namespace Maroik.Core.Domain.Board;

/// <summary>
/// Aggregate root representing a comment on a <see cref="Board"/> post.
/// Persisted independently via its own <c>IBoardCommentRepository</c> (not hydrated or cascaded
/// through the <see cref="Board"/> aggregate), which is why it is an aggregate root rather than a
/// child entity; it references its post by <see cref="BoardId"/> only (no object reference).
/// Supports soft-deletion; deleted comments are excluded entirely from reads rather than shown as a placeholder.
/// </summary>
public sealed class BoardComment : AggregateRoot<long>
{
    /// <summary>ID of the parent board post.</summary>
    public long BoardId { get; private set; }

    /// <summary>Display order of the comment within the post.</summary>
    public long Order { get; private set; }

    /// <summary>Relative path to the commenter's avatar image.</summary>
    public string? AvatarImagePath { get; private set; }

    /// <summary>Nickname of the comment author.</summary>
    public string Writer { get; }

    /// <summary>Comment body text.</summary>
    public string? Content { get; private set; }

    /// <summary>UTC timestamp when the comment was posted.</summary>
    public DateTime Created { get; private set; }

    /// <summary>Soft-delete flag.</summary>
    public bool Deleted { get; private set; }

    private BoardComment(
        long id,
        long boardId,
        long order,
        string? avatarImagePath,
        string writer,
        string? content) : base(id)
    {
        BoardId = boardId;
        Order = order;
        AvatarImagePath = avatarImagePath;
        Writer = writer;
        Content = content;
        Created = DateTime.UtcNow;
    }

    /// <summary>
    /// Rebuilds a <see cref="BoardComment"/> from trusted raw values from the repository layer.
    /// Bypasses validation — use only with data from the trusted repository layer.
    /// </summary>
    public static BoardComment Reconstitute(
        long id,
        long boardId,
        long order,
        string? avatarImagePath,
        string writer,
        string? content,
        DateTime created,
        bool deleted)
    {
        var comment = new BoardComment(id, boardId, order, avatarImagePath, writer, content)
        {
            Created = created, Deleted = deleted
        };
        return comment;
    }

    /// <summary>Creates a new comment after validating required fields.</summary>
    public static ErrorOr<BoardComment> Create(
        long boardId,
        long order,
        string? avatarImagePath,
        string? writer,
        string? content)
    {
        if (string.IsNullOrWhiteSpace(writer))
            return LocalizableError.Validation("BoardComment.WriterEmpty", "Comment author (writer) cannot be empty.");

        if (string.IsNullOrWhiteSpace(content))
            return LocalizableError.Validation("BoardComment.ContentEmpty", "Comment content cannot be empty.");

        // Persisted in a text column, so the database would accept any size; bound it here to the same
        // limit a post's body has, so one comment cannot be used to store or render an unbounded blob.
        if (content.Length > TitledContentPolicy.MaxBodyLength)
            return LocalizableError.Validation("BoardComment.ContentTooLong", "Comment must be {0} characters or fewer.", TitledContentPolicy.MaxBodyLength);

        return new BoardComment(0, boardId, order, avatarImagePath, writer, content);
    }

    /// <summary>Marks the comment as soft-deleted (content is retained in the database).</summary>
    public ErrorOr<Success> SoftDelete()
    {
        if (Deleted)
            return LocalizableError.Conflict("BoardComment.AlreadyDeleted", "Comment is already deleted.");

        Deleted = true;
        return Result.Success;
    }

    /// <summary>Returns true when <paramref name="nickname"/> is this comment's author.</summary>
    public bool IsOwnedBy(string nickname) => Board.IsOwnedBy(Writer, nickname);

    /// <summary>Returns true when <paramref name="nickname"/> may delete this comment (owner, or any admin).</summary>
    public bool CanBeDeletedBy(string nickname, bool isAdmin) => IsOwnedBy(nickname) || isAdmin;
}
