// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Website.Models.ViewModels.Kanbans;

/// <summary>The user's Kanban board as <c>Kanban/UserIndex</c> renders it: the four fixed lanes in order.</summary>
public class KanbanBoardViewModel
{
    /// <summary>The lanes, in display order.</summary>
    public List<KanbanLaneViewModel> Lanes { get; set; } = [];
}
