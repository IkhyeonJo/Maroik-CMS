// ReSharper disable PropertyCanBeMadeInitOnly.Global
using Maroik.Core.Contract.Dtos;

namespace Maroik.Website.Models.ViewModels.Management;

/// <summary>
/// Aggregated output model for the PrivateNote (admin-only private board) page in the management area.
/// Mirrors <c>Forum.FreeForumOutputViewModel</c> in structure but is scoped to the Management area
/// so admin-only routing and authorization filters apply.
/// Contains the paginated post list, the currently viewed post's detail, search state,
/// and all auxiliary data (accounts, comment counts, attachments) needed to render
/// the list and detail panel in a single request.
/// </summary>
public class PrivateNoteOutputViewModel
{
    /// <summary>
    /// The current view mode / action being rendered (e.g. "Index", "Detail", "Edit").
    /// Drives which partial view is shown inside the board layout.
    /// </summary>
    public string? Method { get; set; }

    /// <summary>Pagination state for the board post list (current page, total pages, page-link window).</summary>
    public Pager? Pager { get; set; }

    /// <summary>Posts for the current page. Private notes have no pinning, so there is no separate notice list.</summary>
    public IEnumerable<BoardResponse> Boards { get; set; } = [];

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

    /// <summary>Full account record of the currently logged-in admin.</summary>
    public AccountResponse LoggedInAccount { get; set; } = new();

    /// <summary>IANA time-zone ID of the logged-in admin, used to display post timestamps in local time.</summary>
    public string LoggedInAccountTimeZoneIanaId { get; set; } = "";

    /// <summary>All registered accounts; used to look up writer nicknames when rendering the post list.</summary>
    public List<AccountResponse> AllAccounts { get; set; } = [];

    /// <summary>Nicknames of every Admin account in <see cref="AllAccounts"/>, for highlighting admin-authored posts/comments.</summary>
    public HashSet<string> AdminNicknames { get; set; } = [];

    /// <summary>Maps each board post ID to its comment count for the comment badge in the post list.</summary>
    public Dictionary<long, int> CommentCountByBoardId { get; set; } = [];

    /// <summary>Maps each board post ID to its list of attached files for the attachment icon in the post list.</summary>
    public Dictionary<long, List<BoardAttachedFileDto>> AttachedFilesByBoardId { get; set; } = [];

    /// <summary>Comments on the currently open detail post, rendered in the detail panel.</summary>
    public List<BoardCommentResponse> DetailBoardComments { get; set; } = [];

    /// <summary>The search field selected by the user (e.g. "Title", "Content", "Writer").</summary>
    public string SelectedSearchType { get; set; } = "";

    /// <summary>The search term entered by the user, preserved across pagination.</summary>
    public string TypedSearchText { get; set; } = "";
}
