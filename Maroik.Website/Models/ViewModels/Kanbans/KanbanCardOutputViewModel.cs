// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Website.Models.ViewModels.Kanbans;

/// <summary>One card as the board renders it: Razor encodes every text, and the edit modal is filled from the card's attributes.</summary>
public class KanbanCardOutputViewModel
{
    /// <summary>Card ID.</summary>
    public long Id { get; set; }

    /// <summary>The card's title.</summary>
    public string? Title { get; set; }

    /// <summary>The card's plain-text description, or null (rendered with its line breaks kept).</summary>
    public string? Description { get; set; }

    /// <summary>The due date as yyyy-MM-dd (Gregorian, every culture), or null when the card has none.</summary>
    public string? DueDate { get; set; }

    /// <summary>True when the due date has passed in the owner's time zone (decided by the server): the date is shown in red.</summary>
    public bool Overdue { get; set; }
}
