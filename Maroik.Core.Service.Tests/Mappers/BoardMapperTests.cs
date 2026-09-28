using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Board;
using Maroik.Core.Service.Mappers;

namespace Maroik.Core.Service.Tests.Mappers;

/// <summary>Unit tests for <see cref="BoardMapper"/>.</summary>
public class BoardMapperTests
{
    /// <summary>To response maps all fields.</summary>
    [Fact]
    public void ToResponse_MapsAllFields()
    {
        var created = new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc);
        var updated = new DateTime(2026, 1, 2, 2, 0, 0, DateTimeKind.Utc);

        Board board = Board.Reconstitute(
            id: 42,
            type: "FreeForum",
            title: "Hello World",
            content: "<p>content</p>",
            writer: "Alice",
            created: created,
            updated: updated,
            view: 99,
            deleted: true,
            locked: true,
            noticed: true);

        BoardResponse response = BoardMapper.ToResponse(board);

        Assert.Equal(42, response.Id);
        Assert.Equal("FreeForum", response.Type);
        Assert.Equal("Hello World", response.Title);
        Assert.Equal("<p>content</p>", response.Content);
        Assert.Equal("Alice", response.Writer);
        Assert.Equal(created, response.Created);
        Assert.Equal(updated, response.Updated);
        Assert.Equal(99, response.View);
        Assert.True(response.Deleted);
        Assert.True(response.Locked);
        Assert.True(response.Noticed);
    }
}
