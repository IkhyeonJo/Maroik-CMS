// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Website.Models.ViewModels.Forum;

/// <summary>
/// Form model for posting a new comment on a board post.
/// Also carries the current page number, which the controller echoes back in its JSON reply so the
/// client script can reload the post detail on that page after the comment is saved.
/// </summary>
public class BoardCommentInputViewModel
{
    /// <summary>Database ID of the board post this comment belongs to.</summary>
    public int BoardId { get; set; }

    /// <summary>Plain-text or HTML body of the comment entered by the user.</summary>
    public string? Content { get; set; }

    /// <summary>
    /// The page number of the board list the user was on when they opened the post detail, echoed back
    /// so the client can return to the same page after the comment is submitted.
    /// </summary>
    public int DetailCurrentPage { get; set; }
}
