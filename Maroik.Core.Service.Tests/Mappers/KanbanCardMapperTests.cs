using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Kanbans;
using Maroik.Core.Service.Mappers;

namespace Maroik.Core.Service.Tests.Mappers;

/// <summary>Unit tests for <see cref="KanbanCardMapper"/>.</summary>
public class KanbanCardMapperTests
{
    /// <summary>The fixed "current time" of these tests.</summary>
    private static readonly DateTime Now = new(2026, 5, 24, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>To response maps every field.</summary>
    [Fact]
    public void ToResponse_MapsAllFields()
    {
        var created = new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc);
        var updated = new DateTime(2026, 1, 2, 2, 0, 0, DateTimeKind.Utc);
        KanbanCard card = KanbanCard.Reconstitute(50, "frank@example.com", "InProgress", "Title", "Body", new DateOnly(2026, 6, 1), created, updated);

        KanbanCardResponse response = KanbanCardMapper.ToResponse(card, Now, "UTC");

        Assert.Equal(50, response.Id);
        Assert.Equal("InProgress", response.Lane);
        Assert.Equal("Title", response.Title);
        Assert.Equal("Body", response.Description);
        Assert.Equal(new DateOnly(2026, 6, 1), response.DueDate);
        Assert.False(response.Overdue);
        Assert.Equal(created, response.Created);
        Assert.Equal(updated, response.Updated);
    }

    /// <summary>The overdue flag is the card's due state at the given time and zone: overdue only, not upcoming or none.</summary>
    [Theory]
    [InlineData(2026, 5, 23, true)]
    [InlineData(2026, 5, 24, false)]
    public void ToResponse_SetsOverdue_FromTheDueState(int year, int month, int day, bool expected)
    {
        KanbanCard card = KanbanCard.Reconstitute(1, "frank@example.com", "ToDo", "Title", null, new DateOnly(year, month, day), Now, Now);

        Assert.Equal(expected, KanbanCardMapper.ToResponse(card, Now, "UTC").Overdue);
    }

    /// <summary>A card without a due date is never overdue.</summary>
    [Fact]
    public void ToResponse_IsNotOverdue_WithoutADueDate()
    {
        KanbanCard card = KanbanCard.Reconstitute(1, "frank@example.com", "ToDo", "Title", null, null, Now, Now);

        KanbanCardResponse response = KanbanCardMapper.ToResponse(card, Now, "UTC");

        Assert.Null(response.DueDate);
        Assert.False(response.Overdue);
    }
}
