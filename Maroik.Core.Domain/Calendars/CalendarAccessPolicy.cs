using System.Diagnostics.CodeAnalysis;

namespace Maroik.Core.Domain.Calendars;

/// <summary>
/// Which calendars and events an account may write to: only those of its own calendars. The caller passes the
/// account's calendars (<c>ownedCalendars</c>) as loaded for the request.
/// </summary>
public static class CalendarAccessPolicy
{
    /// <summary>Whether the calendar <paramref name="calendarId"/> is one of <paramref name="ownedCalendars"/>.</summary>
    public static bool Owns(IEnumerable<Calendar> ownedCalendars, long calendarId) => ownedCalendars.Any(c => c.Id == calendarId);

    /// <summary>Whether every one of <paramref name="calendarIds"/> is one of <paramref name="ownedCalendars"/>.</summary>
    public static bool OwnsAll(IEnumerable<Calendar> ownedCalendars, IEnumerable<long> calendarIds) =>
        calendarIds.All(calendarId => Owns(ownedCalendars, calendarId));

    /// <summary>Whether <paramref name="calendarEvent"/> exists and sits in one of <paramref name="ownedCalendars"/>.</summary>
    public static bool OwnsEvent(IEnumerable<Calendar> ownedCalendars, [NotNullWhen(true)] CalendarEvent? calendarEvent) =>
        calendarEvent != null && Owns(ownedCalendars, calendarEvent.CalendarId);
}
