namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Parameters for a paged, filtered board list query — see
/// <see cref="Interfaces.IBoardRepository.QueryPageAsync"/> and
/// <see cref="Interfaces.IBoardService.GetBoardPageAsync"/>.
/// </summary>
/// <param name="Type">Board type to list (e.g. "FreeForum", "PrivateNote").</param>
/// <param name="OwnerNickname">
/// When set, only posts written by this nickname are considered at all (PrivateNote: each user only
/// ever sees their own posts). Applied before search, and — matching the current
/// ManagementController behavior this replaces — when set, Title/Writer search apply no further
/// Locked-visibility split, since every candidate row already belongs to the caller.
/// </param>
/// <param name="SearchType">"Title", "Writer", or null/empty for no search.</param>
/// <param name="SearchText">Search text; the search is skipped if this is null or empty.</param>
/// <param name="IsLoggedIn">Whether the viewer has an active session. Ignored when <see cref="OwnerNickname"/> is set.</param>
/// <param name="ViewerRole">
/// The viewer's role (<see cref="Domain.Account.Role"/>); ignored when not logged in or when
/// <see cref="OwnerNickname"/> is set.
/// </param>
/// <param name="ViewerNickname">
/// The viewer's nickname — used for the Title-search "own locked posts" exception (Admin sees all
/// matches including locked; a regular User sees non-locked matches plus their own locked matches;
/// an anonymous/other viewer sees only non-locked matches). Ignored when <see cref="OwnerNickname"/> is set.
/// </param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Rows per page.</param>
public sealed record BoardPageQuery(
    string Type,
    string? OwnerNickname,
    string? SearchType,
    string? SearchText,
    bool IsLoggedIn,
    string? ViewerRole,
    string? ViewerNickname,
    int Page,
    int PageSize);
