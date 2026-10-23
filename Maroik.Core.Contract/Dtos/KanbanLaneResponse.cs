// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>One lane of a user's Kanban board: the lane and its cards, top to bottom.</summary>
public class KanbanLaneResponse
{
    /// <summary>The lane (a <c>KanbanLanes.Ordered</c> value).</summary>
    public string? Lane { get; set; }

    /// <summary>The lane's cards in display order (creation order, ties by id).</summary>
    public List<KanbanCardResponse> Cards { get; set; } = [];
}
