using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Extensions;
using Maroik.Website.Models.ViewModels.Boards;

namespace Maroik.Website.Mappers;

/// <summary>
/// Builds the rows a post list and a comment thread draw. The controller passes in what it decided for the viewer (whether a title
/// is visible, whether a comment may be deleted — from the domain's rules via <see cref="AccountResponseExtensions"/> and
/// <see cref="BoardResponseExtensions"/>), so the views only render flags.
/// </summary>
public static class BoardViewModelMapper
{
    extension(BoardResponse board)
    {
        /// <summary>
        /// The list row for this post: <paramref name="titleVisible"/> as decided for the viewer, the writer marked when
        /// <paramref name="adminNicknames"/> holds it, and a paperclip when <paramref name="attachedFile"/> names a file.
        /// </summary>
        public BoardListRowViewModel ToListRow(bool titleVisible, HashSet<string> adminNicknames, int commentCount, BoardAttachedFileDto? attachedFile) => new()
        {
            Id = board.Id,
            Title = board.Title,
            Writer = board.Writer,
            Locked = board.Locked,
            View = board.View,
            Created = board.Created,
            TitleVisible = titleVisible,
            IsWriterAdmin = board.Writer.IsAdmin(adminNicknames),
            CommentCount = commentCount,
            HasAttachment = !string.IsNullOrEmpty(attachedFile?.Name)
        };
    }

    extension(BoardCommentResponse comment)
    {
        /// <summary>
        /// The thread row for this comment: <paramref name="canDelete"/> as decided for the viewer, highlighted when
        /// <paramref name="postWriter"/> wrote it, and the writer marked when <paramref name="adminNicknames"/> holds it.
        /// </summary>
        public BoardCommentRowViewModel ToCommentRow(string? postWriter, bool canDelete, HashSet<string> adminNicknames) => new()
        {
            Id = comment.Id,
            Writer = comment.Writer,
            AvatarImagePath = comment.AvatarImagePath,
            Content = comment.Content,
            Created = comment.Created,
            IsWriterAdmin = comment.Writer.IsAdmin(adminNicknames),
            IsByPostWriter = postWriter == comment.Writer,
            CanDelete = canDelete
        };
    }
}
