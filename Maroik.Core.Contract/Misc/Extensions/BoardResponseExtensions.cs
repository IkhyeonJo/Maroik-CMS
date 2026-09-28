using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Account;
using Maroik.Core.Domain.Board;

namespace Maroik.Core.Contract.Misc.Extensions;

/// <summary>
/// Extension methods encoding the "can this viewer see a locked post's title" rule for list views.
/// FreeForum posts grant visibility to the writer or any Admin when unlocked-or-owned;
/// PrivateNote posts are stricter and grant visibility to the writer only (see
/// <c>Management.PrivateNote.cshtml</c>). Both delegate to <see cref="Board.CanBeViewedBy(string?, bool, bool, string?, string?, bool)"/>
/// (Maroik.Core.Domain) — the same field-level rule <c>IBoardService.CanView</c> enforces
/// server-side — rather than re-deriving the rule here, so the two can't drift apart.
/// </summary>
public static class BoardResponseExtensions
{
    extension(BoardResponse board)
    {
        /// <summary>True when this viewer (or any Admin) may see the post per <see cref="Board.CanBeViewedBy(string?, bool, bool, string?, string?, bool)"/>.</summary>
        public bool IsTitleVisibleToOwnerOrAdmin(AccountResponse viewer) =>
            Board.CanBeViewedBy(board.Type, board.Deleted, board.Locked, board.Writer, viewer.Nickname, viewer.Role == Role.Admin);

        /// <summary>True when this viewer (writer only, no admin bypass) may see the post per <see cref="Board.CanBeViewedBy(string?, bool, bool, string?, string?, bool)"/>.</summary>
        public bool IsTitleVisibleToOwner(AccountResponse viewer) =>
            Board.CanBeViewedBy(board.Type, board.Deleted, board.Locked, board.Writer, viewer.Nickname, viewerIsAdmin: false);
    }
}
