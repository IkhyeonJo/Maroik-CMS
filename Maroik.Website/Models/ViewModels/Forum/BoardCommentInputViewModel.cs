// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Website.Models.ViewModels.Forum;

/// <summary>
/// Form model for posting a new comment on a board post.
/// Also carries the current page number so the controller can redirect back
/// to the correct page of the post list after the comment is saved.
/// </summary>
public class BoardCommentInputViewModel
{
    /// <summary>Database ID of the board post this comment belongs to.</summary>
    public int BoardId { get; set; }

    /// <summary>Plain-text or HTML body of the comment entered by the user.</summary>
    public string? Content { get; set; }

    /// <summary>
    /// The page number of the board list the user was on when they opened the post detail,
    /// used to redirect back to the same page after the comment is submitted.
    /// </summary>
    public int DetailCurrentPage { get; set; }
}
