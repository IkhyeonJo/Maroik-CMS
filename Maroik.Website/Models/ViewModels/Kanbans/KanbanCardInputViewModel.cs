// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Website.Models.ViewModels.Kanbans;

/// <summary>
/// Form model for creating a Kanban card from the card modal. <see cref="Lane"/> is the lane whose "+ Add card" button opened the
/// modal (the modal itself has no lane field). The content rules are the domain's (<c>KanbanCard</c>); this model only carries the
/// values.
/// </summary>
public class KanbanCardInputViewModel
{
    /// <summary>The lane the card is created in (e.g. "ToDo").</summary>
    public string? Lane { get; set; }

    /// <summary>The card's title.</summary>
    public string? Title { get; set; }

    /// <summary>The card's plain-text description (optional).</summary>
    public string? Description { get; set; }

    /// <summary>The card's due date, sent as a yyyy-MM-dd JSON string or null; any other shape fails model binding ("Input is invalid").</summary>
    public DateOnly? DueDate { get; set; }
}
