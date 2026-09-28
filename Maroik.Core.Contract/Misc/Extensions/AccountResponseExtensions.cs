using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Account;
using Maroik.Core.Domain.Board;

namespace Maroik.Core.Contract.Misc.Extensions;

/// <summary>
/// Extension methods encoding the "owner" and "owner-or-admin" authorization rules
/// used to gate edit/delete UI affordances for board posts, comments, and private notes.
/// The ownership comparison itself delegates to <see cref="Board.IsOwnedBy(string?, string?)"/> —
/// the same field-level rule <c>Board.IsOwnedBy</c>/<c>BoardComment.IsOwnedBy</c> use — so it can't
/// drift from the domain's definition of "owner." The surrounding admin-bypass / view-gating logic
/// is not reused directly, because by the time a view is rendering, the data has already crossed
/// the service boundary into <see cref="Dtos.BoardResponse"/>/<see cref="Dtos.BoardCommentResponse"/> —
/// the domain entities are gone. Real authorization is enforced server-side by
/// <c>BoardService</c> calling the domain methods; this class only decides what the UI shows.
/// </summary>
public static class AccountResponseExtensions
{
    extension(AccountResponse viewer)
    {
        /// <summary>True when <paramref name="contentWriter"/> is this viewer's own nickname.</summary>
        public bool IsOwner(string? contentWriter) => Board.IsOwnedBy(contentWriter, viewer.Nickname);

        /// <summary>True when the viewer wrote the content, or the viewer is an Admin.</summary>
        public bool IsOwnerOrAdmin(string? contentWriter) => Board.IsOwnedBy(contentWriter, viewer.Nickname) || viewer.Role == Role.Admin;

        /// <summary>
        /// True when this viewer should see local (time-zone-converted) timestamps rather than raw
        /// UTC. Delegates to <see cref="AccountViewPolicy.SeesLocalTime"/> so every call site — view
        /// or otherwise — shares one definition of the role→visibility rule.
        /// </summary>
        public bool SeesLocalTime() => AccountViewPolicy.SeesLocalTime(viewer.Role);
    }

    extension(IEnumerable<AccountResponse> accounts)
    {
        /// <summary>Nicknames of every Admin account in the collection, for O(1) "is this writer an admin?" lookups.</summary>
        public HashSet<string> ToAdminNicknameSet() =>
        [
            .. accounts.Where(account => account.Role == Role.Admin && !string.IsNullOrEmpty(account.Nickname))
                .Select(account => account.Nickname!)
        ];
    }

    extension(string? nickname)
    {
        /// <summary>True when <paramref name="nickname"/> belongs to an Admin account, per a set built by <see cref="ToAdminNicknameSet"/>.</summary>
        public bool IsAdmin(HashSet<string> adminNicknames) => adminNicknames.Contains(nickname ?? "");
    }
}
