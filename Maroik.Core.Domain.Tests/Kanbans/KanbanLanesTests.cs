using Maroik.Core.Domain.Kanbans;
namespace Maroik.Core.Domain.Tests.Kanbans;

/// <summary>
/// Unit tests for <see cref="KanbanLanes"/>: the four fixed lanes of a Kanban board, in their display order, and the
/// membership check the card factory and the DB's <c>KanbanCard_Lane_check</c> both mirror.
/// </summary>
public class KanbanLanesTests
{
    /// <summary>The board has exactly four lanes, in the order Backlog, To do, In progress, Done.</summary>
    [Fact]
    public void Ordered_ListsTheFourLanes_InTheirDisplayOrder()
    {
        Assert.Equal(["Backlog", "ToDo", "InProgress", "Done"], KanbanLanes.Ordered);
    }

    /// <summary>The stored lane values are the <see cref="KanbanLane"/> member names.</summary>
    [Fact]
    public void Ordered_IsTheKanbanLaneMemberNames()
    {
        Assert.Equal(Enum.GetNames<KanbanLane>(), KanbanLanes.Ordered);
    }

    /// <summary>IsKnown accepts each of the four lanes.</summary>
    [Theory]
    [InlineData("Backlog")]
    [InlineData("ToDo")]
    [InlineData("InProgress")]
    [InlineData("Done")]
    public void IsKnown_KnownLane_ReturnsTrue(string lane) => Assert.True(KanbanLanes.IsKnown(lane));

    /// <summary>IsKnown refuses an unknown value, a differently-cased one, the display label and null.</summary>
    [Theory]
    [InlineData("Archive")]
    [InlineData("backlog")]
    [InlineData("To do")]
    [InlineData("")]
    [InlineData(null)]
    public void IsKnown_UnknownOrNull_ReturnsFalse(string? lane) => Assert.False(KanbanLanes.IsKnown(lane));
}
