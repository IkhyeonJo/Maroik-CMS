using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Board;
using Maroik.Core.Service.Mappers;

namespace Maroik.Core.Service.Tests.Mappers;

/// <summary>Unit tests for <see cref="BoardCommentMapper"/>.</summary>
public class BoardCommentMapperTests
{
    /// <summary>To response maps all fields.</summary>
    [Fact]
    public void ToResponse_MapsAllFields()
    {
        var created = new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc);

        BoardComment comment = BoardComment.Reconstitute(
            id: 5,
            boardId: 42,
            order: 1,
            avatarImagePath: "/avatars/bob.png",
            writer: "Bob",
            content: "Nice post!",
            created: created,
            deleted: true);

        BoardCommentResponse response = BoardCommentMapper.ToResponse(comment);

        Assert.Equal(5, response.Id);
        Assert.Equal(42, response.BoardId);
        Assert.Equal(1, response.Order);
        Assert.Equal("/avatars/bob.png", response.AvatarImagePath);
        Assert.Equal("Bob", response.Writer);
        Assert.Equal("Nice post!", response.Content);
        Assert.Equal(created, response.Created);
        Assert.True(response.Deleted);
    }
}
