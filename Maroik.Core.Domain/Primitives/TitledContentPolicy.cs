namespace Maroik.Core.Domain.Primitives;

/// <summary>
/// Shared length limits for a title + free-text body pair, used by aggregates that combine both
/// (<see cref="Board.Board"/>'s Title/Content, <see cref="Calendar.CalendarEvent"/>'s
/// Title/Description). Centralized so the Create/Update methods on both aggregates can't drift
/// apart from each other or from the persisted column shapes they both share.
/// </summary>
public static class TitledContentPolicy
{
    /// <summary>Maximum length, in characters, of a title.</summary>
    public const int MaxTitleLength = 100;

    /// <summary>Maximum length, in characters, of the free-text body (content/description).</summary>
    public const int MaxBodyLength = 16384;
}
