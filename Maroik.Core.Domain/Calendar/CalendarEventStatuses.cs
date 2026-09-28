using Maroik.Core.Domain.Primitives;

namespace Maroik.Core.Domain.Calendar;

/// <summary>
/// Canonical availability-status strings stored verbatim in <see cref="CalendarEvent.Status"/>.
/// Mirrors the DB's <c>CalendarEvent_Status_check</c> constraint (<c>Busy</c> / <c>Free</c> only) —
/// this is the single source of truth for the value across every layer, following the same pattern
/// as <see cref="Maroik.Core.Domain.Board.BoardTypes"/>. <see langword="null"/> is a separate,
/// always-allowed case handled by callers (the repository maps it to <see cref="Busy"/> before
/// persisting), not part of this taxonomy.
/// </summary>
public static class CalendarEventStatuses
{
    /// <summary>The subscriber appears unavailable for the event's duration.</summary>
    public const string Busy = "Busy";

    /// <summary>The subscriber appears available for the event's duration.</summary>
    public const string Free = "Free";

    private static readonly StringTaxonomy _taxonomy = new(Busy, Free);

    /// <summary>Every recognized status value.</summary>
    public static IReadOnlySet<string> All => _taxonomy.All;

    /// <summary>Returns true when <paramref name="status"/> is one of the known status values.</summary>
    public static bool IsKnown(string? status) => _taxonomy.IsKnown(status);
}
