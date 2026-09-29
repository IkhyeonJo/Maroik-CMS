// ReSharper disable PropertyCanBeMadeInitOnly.Global
using Maroik.Core.Contract.Dtos;

namespace Maroik.Website.Models.ViewModels.Forum;

/// <summary>
/// Aggregated output model for the FreeForum (public board) page.
/// Contains the paginated post list, the currently viewed post's detail,
/// search state, and all auxiliary data (accounts, comment counts, attachments)
/// needed to render the list and detail panel in a single request.
/// </summary>
public class FreeForumOutputViewModel
{
    /// <summary>
    /// The current view mode being rendered — the page's <c>method</c> query value ("list", "write", "detail" or "edit").
    /// Drives which partial view is shown inside the forum layout.
    /// </summary>
    public string? Method { get; set; }

    /// <summary>Pagination state for the board post list (current page, total pages, page-link window).</summary>
    public Pager? Pager { get; set; }

    /// <summary>Pinned (noticed) posts displayed at the top of the board above the regular post list.</summary>
    public IEnumerable<BoardResponse> NoticeBoards { get; set; } = [];

    /// <summary>Regular (non-pinned) posts for the current page.</summary>
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

    /// <summary>Full account record of the currently logged-in user.</summary>
    public AccountResponse LoggedInAccount { get; set; } = new();

    /// <summary>IANA time-zone ID of the logged-in user, used to display post timestamps in local time.</summary>
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

    /// <summary>The search field selected by the user ("Title" or "Writer"; empty for no search).</summary>
    public string SelectedSearchType { get; set; } = "";

    /// <summary>The search term entered by the user, preserved across pagination.</summary>
    public string TypedSearchText { get; set; } = "";
}
