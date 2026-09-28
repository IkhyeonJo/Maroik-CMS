// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Data transfer object returned when reading a board comment.
/// </summary>
public class BoardCommentResponse
{
    /// <summary>Comment ID (primary key).</summary>
    public long Id { get; set; }

    /// <summary>ID of the parent board post.</summary>
    public long BoardId { get; set; }

    /// <summary>Display order within the post.</summary>
    public long Order { get; set; }

    /// <summary>Relative path to the commenter's avatar image.</summary>
    public string? AvatarImagePath { get; set; }

    /// <summary>Nickname of the comment author.</summary>
    public string? Writer { get; set; }

    /// <summary>Comment body text.</summary>
    public string? Content { get; set; }

    /// <summary>UTC timestamp when the comment was posted.</summary>
    public DateTime Created { get; set; }

    /// <summary>Soft-delete flag.</summary>
    public bool Deleted { get; set; }
}
