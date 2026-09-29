// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Data transfer object used to create a comment on a board post (deletion takes only the comment ID).
/// </summary>
public class BoardCommentRequest
{
    /// <summary>ID of the parent board post this comment belongs to.</summary>
    public long BoardId { get; set; }

    /// <summary>Relative path to the commenter's avatar image.</summary>
    public string? AvatarImagePath { get; set; }

    /// <summary>Nickname of the comment author.</summary>
    public string? Writer { get; set; }

    /// <summary>Comment body text.</summary>
    public string? Content { get; set; }
}
