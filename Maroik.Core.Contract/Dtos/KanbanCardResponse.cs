// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>Data transfer object returned for one card of a Kanban board.</summary>
public class KanbanCardResponse
{
    /// <summary>Card ID (primary key).</summary>
    public long Id { get; set; }

    /// <summary>The lane the card lives in.</summary>
    public string? Lane { get; set; }

    /// <summary>The card's title.</summary>
    public string? Title { get; set; }

    /// <summary>The card's plain-text description, or null.</summary>
    public string? Description { get; set; }

    /// <summary>The card's due date, or null.</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>True when the due date has passed in the owner's time zone (decided by <c>KanbanCard.DueStateAt</c>).</summary>
    public bool Overdue { get; set; }

    /// <summary>UTC creation timestamp.</summary>
    public DateTime Created { get; set; }

    /// <summary>UTC last-updated timestamp.</summary>
    public DateTime Updated { get; set; }
}
