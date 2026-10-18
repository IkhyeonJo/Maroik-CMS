using Maroik.Core.Contract.Dtos;
using Maroik.Website.Mappers;
using Maroik.Website.Models.ViewModels.Boards;

namespace Maroik.Website.Tests.Mappers;

/// <summary>Unit tests for <see cref="BoardViewModelMapper"/>: the list and comment rows carry the viewer's flags, not the raw data.</summary>
public class BoardViewModelMapperTests
{
    /// <summary>Administrators among the writers on the page.</summary>
    private static readonly HashSet<string> _admins = ["Root"];

    /// <summary>A locked post by <paramref name="writer"/>.</summary>
    private static BoardResponse Post(string writer) => new()
    {
        Id = 5, Title = "Title", Writer = writer, Locked = true, View = 12, Created = new DateTime(2026, 7, 1, 9, 0, 0, DateTimeKind.Utc)
    };

    /// <summary>A comment by <paramref name="writer"/>.</summary>
    private static BoardCommentResponse Comment(string writer) => new()
    {
        Id = 8, Writer = writer, AvatarImagePath = "/a.png", Content = "Hi &amp; bye", Created = new DateTime(2026, 7, 2, 9, 0, 0, DateTimeKind.Utc)
    };

    /// <summary>A list row copies the post's display fields and takes the visibility, the comment count and the attachment as given.</summary>
    [Fact]
    public void ToListRow_CarriesThePostAndTheViewersFlags()
    {
        BoardListRowViewModel row = Post("Alice").ToListRow(titleVisible: true, _admins, commentCount: 3, new BoardAttachedFileDto { Name = "a" });

        Assert.Equal(5, row.Id);
        Assert.Equal("Title", row.Title);
        Assert.Equal("Alice", row.Writer);
        Assert.True(row.Locked);
        Assert.Equal(12, row.View);
        Assert.Equal(new DateTime(2026, 7, 1, 9, 0, 0, DateTimeKind.Utc), row.Created);
        Assert.True(row.TitleVisible);
        Assert.False(row.IsWriterAdmin);
        Assert.Equal(3, row.CommentCount);
        Assert.True(row.HasAttachment);
    }

    /// <summary>A hidden title stays hidden, and an administrator writer is marked.</summary>
    [Fact]
    public void ToListRow_HidesTheTitle_AndMarksAnAdminWriter()
    {
        BoardListRowViewModel row = Post("Root").ToListRow(titleVisible: false, _admins, commentCount: 0, attachedFile: null);

        Assert.False(row.TitleVisible);
        Assert.True(row.IsWriterAdmin);
    }

    /// <summary>No attachment, or an attachment record without a name, shows no paperclip.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ToListRow_HasNoAttachment_WithoutANamedFile(bool emptyRecord)
    {
        BoardListRowViewModel row = Post("Alice").ToListRow(true, _admins, 0, emptyRecord ? new BoardAttachedFileDto { Name = "" } : null);

        Assert.False(row.HasAttachment);
    }

    /// <summary>A comment row copies the comment and takes the delete permission as given; the post writer's own comment is marked.</summary>
    [Fact]
    public void ToCommentRow_CarriesTheCommentAndTheViewersFlags()
    {
        BoardCommentRowViewModel row = Comment("Alice").ToCommentRow(postWriter: "Alice", canDelete: true, _admins);

        Assert.Equal(8, row.Id);
        Assert.Equal("Alice", row.Writer);
        Assert.Equal("/a.png", row.AvatarImagePath);
        Assert.Equal("Hi &amp; bye", row.Content);
        Assert.Equal(new DateTime(2026, 7, 2, 9, 0, 0, DateTimeKind.Utc), row.Created);
        Assert.True(row.CanDelete);
        Assert.True(row.IsByPostWriter);
        Assert.False(row.IsWriterAdmin);
    }

    /// <summary>Someone else's comment is not marked as the post writer's, an administrator's is marked, and a refused delete stays refused.</summary>
    [Fact]
    public void ToCommentRow_ForAnotherWriter_AndAnAdmin()
    {
        BoardCommentRowViewModel row = Comment("Root").ToCommentRow(postWriter: "Alice", canDelete: false, _admins);

        Assert.False(row.IsByPostWriter);
        Assert.True(row.IsWriterAdmin);
        Assert.False(row.CanDelete);
    }
}
