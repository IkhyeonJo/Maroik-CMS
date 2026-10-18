// ReSharper disable PropertyCanBeMadeInitOnly.Global
namespace Maroik.Website.Models.ViewModels.Boards;

/// <summary>
/// One row of a post list (free forum or private notes) as the page draws it. Whether the title may be shown and whether the
/// writer is an administrator are decided by the controller (<see cref="Mappers.BoardViewModelMapper"/>), not by the view.
/// </summary>
public class BoardListRowViewModel
{
    /// <summary>Post id, for the detail link.</summary>
    public long Id { get; set; }

    /// <summary>Post title; shown only when <see cref="TitleVisible"/>.</summary>
    public string? Title { get; set; }

    /// <summary>Writer's nickname.</summary>
    public string? Writer { get; set; }

    /// <summary>True when the post is locked (the row shows a lock icon).</summary>
    public bool Locked { get; set; }

    /// <summary>View count.</summary>
    public long View { get; set; }

    /// <summary>Creation time in UTC; the view formats it for the viewer.</summary>
    public DateTime Created { get; set; }

    /// <summary>True when this viewer may see the title (and open the post); otherwise the row says who can.</summary>
    public bool TitleVisible { get; set; }

    /// <summary>True when the writer is an administrator (the name is highlighted).</summary>
    public bool IsWriterAdmin { get; set; }

    /// <summary>Number of the post's comments, shown after the title.</summary>
    public int CommentCount { get; set; }

    /// <summary>True when the post has an attachment (the row shows a paperclip).</summary>
    public bool HasAttachment { get; set; }
}
