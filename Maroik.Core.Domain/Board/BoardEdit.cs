namespace Maroik.Core.Domain.Board;

/// <summary>What <see cref="Board.EditBy"/> did with an edit request.</summary>
public enum BoardEdit
{
    /// <summary>The requester may not edit this post; nothing changed.</summary>
    Refused,

    /// <summary>The author edited the title / content and set the lock as requested.</summary>
    ContentEdited,

    /// <summary>An administrator who is not the author cleared the post's lock; title and content are untouched.</summary>
    LockCleared,
}
