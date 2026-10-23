// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Data transfer object used to create a Kanban card in one of the four fixed lanes. The lane is the one whose "+ Add card" button
/// was clicked; after creation it can never change (K11).
/// </summary>
public class KanbanCardCreateRequest
{
    /// <summary>The lane the card is created in (a <c>KanbanLanes.Ordered</c> value, e.g. "ToDo").</summary>
    public string? Lane { get; set; }

    /// <summary>The card's title (required).</summary>
    public string? Title { get; set; }

    /// <summary>Optional plain-text description.</summary>
    public string? Description { get; set; }

    /// <summary>Optional due date (a date only).</summary>
    public DateOnly? DueDate { get; set; }
}
