// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Website.Models.ViewModels.Kanbans;

/// <summary>
/// Form model for editing a Kanban card from the card modal. It has no lane: a card never leaves the lane it was created in (K11),
/// so a lane sent along with an edit is not even bound.
/// </summary>
public class KanbanCardEditInputViewModel
{
    /// <summary>The ID of the card to edit.</summary>
    public long Id { get; set; }

    /// <summary>The card's new title.</summary>
    public string? Title { get; set; }

    /// <summary>The card's new plain-text description (none when blank).</summary>
    public string? Description { get; set; }

    /// <summary>The card's new due date, sent as a yyyy-MM-dd JSON string or null; any other shape fails model binding ("Input is invalid").</summary>
    public DateOnly? DueDate { get; set; }
}
