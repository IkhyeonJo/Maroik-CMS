namespace Maroik.Core.Domain.Kanbans;

/// <summary>
/// Lays a user's cards out as their board: the four fixed lanes in order (K5), each holding its cards in creation order — the oldest
/// on top, a new card at the bottom, ties broken by id (K12). The order is not user-changeable, so no position is stored.
/// </summary>
public static class KanbanBoardPolicy
{
    /// <summary>
    /// Groups <paramref name="cards"/> by lane. Every lane is present even when empty, so a user with no cards sees four empty lanes
    /// (K3). A card whose lane is not one of the four (impossible past <see cref="KanbanCard.Create"/> and the DB CHECK) is left out.
    /// </summary>
    public static IReadOnlyList<KanbanLaneCards> Arrange(IEnumerable<KanbanCard> cards)
    {
        ILookup<string, KanbanCard> byLane = cards.ToLookup(c => c.Lane, StringComparer.Ordinal);
        return
        [
            .. KanbanLanes.Ordered.Select(lane => new KanbanLaneCards(lane,
                [.. byLane[lane].OrderBy(c => c.Created).ThenBy(c => c.Id)]))
        ];
    }
}
