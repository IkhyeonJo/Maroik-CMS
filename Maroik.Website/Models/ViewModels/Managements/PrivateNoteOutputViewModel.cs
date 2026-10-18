// ReSharper disable PropertyCanBeMadeInitOnly.Global
using Maroik.Core.Contract.Dtos;
using Maroik.Website.Models.ViewModels.Boards;

namespace Maroik.Website.Models.ViewModels.Managements;

/// <summary>
/// Aggregated output model for the PrivateNote page in the Management area — each signed-in account's
/// (Admin or User) own private notes, visible to their author only. Mirrors
/// <c>Forum.FreeForumOutputViewModel</c> in structure, minus the pinned-notice list.
/// Contains the paginated post list, the currently viewed post's detail, search state,
/// and all auxiliary data (accounts, comment counts, attachments) needed to render
/// the list and detail panel in a single request.
/// </summary>
public class PrivateNoteOutputViewModel
{
    /// <summary>
    /// The current view mode being rendered — the page's <c>method</c> query value ("list", "write", "detail" or "edit").
    /// Drives which partial view is shown inside the board layout.
    /// </summary>
    public string? Method { get; set; }

    /// <summary>Pagination state for the board post list (current page, total pages, page-link window).</summary>
    public Pager? Pager { get; set; }

    /// <summary>Posts for the current page. Private notes have no pinning, so there is no separate notice list.</summary>
    public List<BoardListRowViewModel> Boards { get; set; } = [];

    /// <summary>Full detail of the post currently open in the detail panel; null when no post is selected.</summary>
    public BoardOutputViewModel? BoardOutputViewModel { get; set; }

    /// <summary>ID of the post currently open in the detail panel; 0 when none.</summary>
    public int DetailBoardId { get; set; }

    /// <summary>Page number the user was on when they opened the detail panel, used for "back" navigation.</summary>
    public int DetailCurrentPage { get; set; }

    /// <summary>ID of the post currently open in the edit form; 0 when none.</summary>
    public int EditBoardId { get; set; }

    /// <summary>Page number the user was on when they opened the edit form, used for "back" navigation.</summary>
    public int EditCurrentPage { get; set; }

    // Populated by controller - consumed by view

    /// <summary>Full account record of the currently logged-in account.</summary>
    public AccountResponse LoggedInAccount { get; set; } = new();

    /// <summary>IANA time-zone ID of the logged-in account, used to display post timestamps in local time.</summary>
    public string LoggedInAccountTimeZoneIanaId { get; set; } = "";

    /// <summary>True when the viewer sees times in their own time zone (<c>AccountViewPolicy.SeesLocalTime</c>), false for UTC.</summary>
    public bool ViewerSeesLocalTime { get; set; }

    /// <summary>Comments on the currently open detail post, rendered in the detail panel.</summary>
    public List<BoardCommentRowViewModel> DetailBoardComments { get; set; } = [];

    /// <summary>The search field selected by the user ("Title" or "Writer"; empty for no search).</summary>
    public string SelectedSearchType { get; set; } = "";

    /// <summary>The search term entered by the user, preserved across pagination.</summary>
    public string TypedSearchText { get; set; } = "";
}
