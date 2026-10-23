namespace Maroik.Core.Domain.Kanbans;

/// <summary>One lane of an arranged board (<see cref="KanbanBoardPolicy.Arrange"/>): the lane and its cards, top to bottom.</summary>
/// <param name="Lane">The lane (a <see cref="KanbanLanes.Ordered"/> value).</param>
/// <param name="Cards">The lane's cards in display order.</param>
public sealed record KanbanLaneCards(string Lane, IReadOnlyList<KanbanCard> Cards);
