// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Data transfer object used to edit an existing Kanban card. It deliberately carries no lane: a card stays in the lane it was
/// created in, so an edit cannot move it (K11).
/// </summary>
public class KanbanCardUpdateRequest
{
    /// <summary>The ID of the card to edit.</summary>
    public long Id { get; set; }

    /// <summary>The card's new title (required).</summary>
    public string? Title { get; set; }

    /// <summary>The card's new plain-text description (none when blank).</summary>
    public string? Description { get; set; }

    /// <summary>The card's new due date (none when null).</summary>
    public DateOnly? DueDate { get; set; }
}
