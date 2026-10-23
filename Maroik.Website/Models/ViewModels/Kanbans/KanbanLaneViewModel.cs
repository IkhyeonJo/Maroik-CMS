// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Website.Models.ViewModels.Kanbans;

/// <summary>One lane of the board: its stored key (also the resx key of its label) and its cards, top to bottom.</summary>
public class KanbanLaneViewModel
{
    /// <summary>The lane key (a <c>KanbanLane</c> member name, e.g. "InProgress").</summary>
    public string Lane { get; set; } = "";

    /// <summary>The lane's cards in display order.</summary>
    public List<KanbanCardOutputViewModel> Cards { get; set; } = [];
}
