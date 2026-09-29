namespace Maroik.Core.Domain.Primitives;

/// <summary>
/// Shared length limit for a single short free-text field backed by a <c>character varying(255)</c>
/// column (<see cref="Account.Account"/>'s Nickname, <see cref="ValueObjects.Email"/>, <see cref="Calendar.Calendar"/>'s Name,
/// <see cref="Calendar.CalendarEvent"/>'s Location, a menu field, an attached file's Name/Extension/Path).
/// Centralized so those bounded contexts' Create/Update methods can't drift apart from each other
/// or from the persisted column width they both share.
/// </summary>
public static class ShortTextPolicy
{
    /// <summary>Maximum length, in characters, of the field.</summary>
    public const int MaxLength = 255;
}
