// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Data transfer object used to create or update a calendar event.
/// Supports all-day events, time-zone-aware date ranges, and optional recurrence.
/// </summary>
public class CalendarEventRequest
{
    /// <summary>Event ID (auto-incremented primary key; 0 for new events).</summary>
    public long Id { get; set; }

    /// <summary>ID of the parent calendar this event belongs to (foreign key).</summary>
    public long CalendarId { get; set; }

    /// <summary>Event title displayed on the calendar.</summary>
    public string? Title { get; set; }

    /// <summary>Optional rich-text description of the event.</summary>
    public string? Description { get; set; }

    /// <summary>When true, the event spans the entire day (no specific start/end times).</summary>
    public bool AllDay { get; set; }

    /// <summary>Event start date/time (stored in UTC; convert using StartDateTimeZoneIanaId for display).</summary>
    public DateTime StartDate { get; set; }

    /// <summary>Event end date/time (stored in UTC; convert using EndDateTimeZoneIanaId for display).</summary>
    public DateTime EndDate { get; set; }

    /// <summary>IANA time-zone ID of the start date (e.g. "America/New_York"). Null means UTC.</summary>
    public string? StartDateTimeZoneIanaId { get; set; }

    /// <summary>IANA time-zone ID of the end date. Null means UTC.</summary>
    public string? EndDateTimeZoneIanaId { get; set; }

    /// <summary>Physical or virtual location of the event.</summary>
    public string? Location { get; set; }

    /// <summary>Availability status shown to other calendar subscribers (e.g. "Busy", "Free").</summary>
    public string? Status { get; set; }

    /// <summary>Optional reference to a CalendarRecurrence row for repeating events.</summary>
    public long? RecurrenceId { get; set; }
}
