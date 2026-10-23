using System.Diagnostics.CodeAnalysis;
using Maroik.Core.Domain.Primitives;

namespace Maroik.Core.Domain.Kanbans;

/// <summary>
/// The lane taxonomy of a Kanban board: <see cref="KanbanLane"/>'s member names, in their declared (display) order. Built from the
/// enum, as <see cref="Finances.AssetItems"/> is, so it can never drift from it. Checked by <see cref="KanbanCard.Create"/> and
/// mirrored by the DB's <c>KanbanCard_Lane_check</c>; the view renders the lanes in this order.
/// </summary>
public static class KanbanLanes
{
    /// <summary>Membership set backing <see cref="IsKnown"/>, built from the enum's member names.</summary>
    private static readonly StringTaxonomy _taxonomy = new(Enum.GetNames<KanbanLane>());

    /// <summary>Every lane, in display order: Backlog, ToDo, InProgress, Done.</summary>
    public static IReadOnlyList<string> Ordered { get; } = Enum.GetNames<KanbanLane>();

    /// <summary>Returns true when <paramref name="lane"/> is one of the four lanes (exact, case-sensitive).</summary>
    public static bool IsKnown([NotNullWhen(true)] string? lane) => _taxonomy.IsKnown(lane);
}
