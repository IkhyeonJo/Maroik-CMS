// ReSharper disable PropertyCanBeMadeInitOnly.Global
using Maroik.Core.Contract.Dtos;

namespace Maroik.Website.Models.ViewModels.Calendar;

/// <summary>
/// Aggregated view model for the main Calendar page.
/// Contains the logged-in user's own calendars and events, subscribed (other) calendars
/// and their events, and all current-time data needed to render the time-grid correctly
/// in the user's local time zone.
/// </summary>
public class CalendarOutputViewModel
{
    /// <summary>Calendars owned by the logged-in user.</summary>
    public IEnumerable<CalendarResponse> Calendars { get; set; } = [];

    /// <summary>Events belonging to the user's own calendars, projected to the display view model.</summary>
    public IEnumerable<CalendarEventOutputViewModel> CalendarEventOutputViewModels { get; set; } = [];

    /// <summary>Calendars that the user has subscribed to (created by other users and shared).</summary>
    public IEnumerable<CalendarResponse> OtherCalendars { get; set; } = [];

    /// <summary>Events belonging to the subscribed calendars, projected to the display view model.</summary>
    public IEnumerable<CalendarEventOutputViewModel> OtherCalendarEventOutputViewModels { get; set; } = [];

    // Populated by controller - consumed by view

    /// <summary>Full account record of the currently logged-in user.</summary>
    public AccountResponse LoggedInAccount { get; set; } = new();

    /// <summary>IANA time-zone ID of the logged-in user (e.g. "Asia/Seoul"), used to convert displayed times.</summary>
    public string LoggedInAccountTimeZoneIanaId { get; set; } = "UTC";

    /// <summary>Today's date string formatted for the calendar header, in the user's local time zone.</summary>
    public string CurrentDate { get; set; } = "";

    /// <summary>List of half-hour interval labels (e.g. "00:00", "00:30") for the day-view time grid.</summary>
    public List<string> TimeIntervals { get; set; } = [];

    /// <summary>The time-grid interval label that matches the user's current local time.</summary>
    public string CurrentInterval { get; set; } = "";

    /// <summary>Current hour component (0–23) in the user's local time zone, used to scroll the time grid.</summary>
    public int CurrentHour { get; set; }

    /// <summary>Current minute component (0–59) in the user's local time zone.</summary>
    public int CurrentMinute { get; set; }

    /// <summary>Current second component (0–59) in the user's local time zone.</summary>
    public int CurrentSecond { get; set; }
}
