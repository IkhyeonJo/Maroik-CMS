namespace Maroik.Core.Domain.Kanbans;

/// <summary>
/// What the due-date text at the bottom right of a card shows (K10), decided by <see cref="KanbanCard.DueStateAt"/> in the
/// owner's time zone. The card itself stays white in every state; only that text changes.
/// </summary>
public enum KanbanDueState
{
    /// <summary>The card has no due date: nothing is shown.</summary>
    None,

    /// <summary>The due date is today or later: the date in small grey text.</summary>
    Upcoming,

    /// <summary>The due date has passed: the date in red text.</summary>
    Overdue
}
