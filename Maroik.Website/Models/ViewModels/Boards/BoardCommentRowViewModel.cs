// ReSharper disable PropertyCanBeMadeInitOnly.Global
namespace Maroik.Website.Models.ViewModels.Boards;

/// <summary>
/// One comment under a post as the page draws it. Who may delete it, whether its writer is an administrator and whether the
/// post's own writer left it are decided by the controller (<see cref="Mappers.BoardViewModelMapper"/>), not by the view.
/// </summary>
public class BoardCommentRowViewModel
{
    /// <summary>Comment id, for the delete link.</summary>
    public long Id { get; set; }

    /// <summary>Writer's nickname.</summary>
    public string? Writer { get; set; }

    /// <summary>Writer's avatar image path.</summary>
    public string? AvatarImagePath { get; set; }

    /// <summary>Stored (HTML-encoded) comment text.</summary>
    public string? Content { get; set; }

    /// <summary>Creation time in UTC; the view formats it for the viewer.</summary>
    public DateTime Created { get; set; }

    /// <summary>True when the writer is an administrator (the name is highlighted).</summary>
    public bool IsWriterAdmin { get; set; }

    /// <summary>True when the post's own writer left the comment (the comment is highlighted).</summary>
    public bool IsByPostWriter { get; set; }

    /// <summary>True when this viewer may delete the comment (the delete link is shown); the server checks again on delete.</summary>
    public bool CanDelete { get; set; }
}
