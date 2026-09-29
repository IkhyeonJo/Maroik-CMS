using Maroik.Core.Domain.Primitives;

namespace Maroik.Core.Domain.Calendar;

/// <summary>
/// Canonical notification-delivery-method strings stored verbatim in
/// <see cref="CalendarEventReminder.Method"/>. Mirrors the DB's
/// <c>CalendarEventReminder_Method_check</c> constraint (<c>Email</c> / <c>Notification</c> only) —
/// this is the single source of truth for the value across every layer, following the same pattern
/// as <see cref="Maroik.Core.Domain.Board.BoardTypes"/>.
/// </summary>
public static class ReminderMethods
{
    /// <summary>Deliver the reminder by e-mail.</summary>
    public const string Email = "Email";

    /// <summary>Deliver the reminder as an in-app/push notification.</summary>
    public const string Notification = "Notification";

    /// <summary>Membership set backing <see cref="All"/> / <see cref="IsKnown"/>.</summary>
    private static readonly StringTaxonomy _taxonomy = new([Email, Notification]);

    /// <summary>Every recognized delivery method.</summary>
    public static IReadOnlySet<string> All => _taxonomy.All;

    /// <summary>Returns true when <paramref name="method"/> is one of the known delivery methods.</summary>
    public static bool IsKnown(string? method) => _taxonomy.IsKnown(method);
}
