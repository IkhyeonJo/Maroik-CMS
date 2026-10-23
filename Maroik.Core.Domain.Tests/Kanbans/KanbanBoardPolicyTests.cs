using Maroik.Core.Domain.Kanbans;
namespace Maroik.Core.Domain.Tests.Kanbans;

/// <summary>
/// Unit tests for <see cref="KanbanBoardPolicy.Arrange"/>: a board is the four fixed lanes in order (K5), each holding its cards
/// in creation order — the oldest on top, ties broken by id (K12) — whatever order the cards arrive in.
/// </summary>
public class KanbanBoardPolicyTests
{
    /// <summary>A card with the given id, lane and creation time.</summary>
    private static KanbanCard Card(long id, string lane, DateTime created) =>
        KanbanCard.Reconstitute(id, "user@example.com", lane, $"Card {id}", null, null, created, created);

    /// <summary>Without cards the board is still the four lanes, each empty (a new user sees four empty lanes, K3).</summary>
    [Fact]
    public void Arrange_ReturnsTheFourEmptyLanes_WhenThereAreNoCards()
    {
        IReadOnlyList<KanbanLaneCards> board = KanbanBoardPolicy.Arrange([]);

        Assert.Equal(KanbanLanes.Ordered, board.Select(l => l.Lane));
        Assert.All(board, lane => Assert.Empty(lane.Cards));
    }

    /// <summary>Each card lands in its own lane, and the lanes keep their fixed order.</summary>
    [Fact]
    public void Arrange_PutsEachCard_InItsLane()
    {
        DateTime t = new(2026, 5, 1);
        IReadOnlyList<KanbanLaneCards> board = KanbanBoardPolicy.Arrange([Card(1, "Done", t), Card(2, "Backlog", t), Card(3, "InProgress", t)]);

        Assert.Equal(["Backlog", "ToDo", "InProgress", "Done"], board.Select(l => l.Lane));
        Assert.Equal([2L], board[0].Cards.Select(c => c.Id));
        Assert.Empty(board[1].Cards);
        Assert.Equal([3L], board[2].Cards.Select(c => c.Id));
        Assert.Equal([1L], board[3].Cards.Select(c => c.Id));
    }

    /// <summary>Inside a lane the oldest card comes first; cards created at the same instant are ordered by id.</summary>
    [Fact]
    public void Arrange_OrdersALane_ByCreation_ThenById()
    {
        DateTime t = new(2026, 5, 1, 9, 0, 0);
        IReadOnlyList<KanbanLaneCards> board = KanbanBoardPolicy.Arrange(
        [
            Card(7, "ToDo", t.AddMinutes(5)),
            Card(9, "ToDo", t),
            Card(4, "ToDo", t),
            Card(1, "ToDo", t.AddMinutes(10)),
        ]);

        Assert.Equal([4L, 9L, 7L, 1L], board[1].Cards.Select(c => c.Id));
    }
}
