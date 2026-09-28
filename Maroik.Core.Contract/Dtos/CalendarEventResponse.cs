// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Data transfer object returned when reading a calendar event.
/// </summary>
public class CalendarEventResponse
{
    /// <summary>Event ID (primary key).</summary>
    public long Id { get; set; }

    /// <summary>ID of the parent calendar.</summary>
    public long CalendarId { get; set; }

    /// <summary>Event title.</summary>
    public string? Title { get; set; }

    /// <summary>Optional rich-text description.</summary>
    public string? Description { get; set; }

    /// <summary>True when the event is an all-day event.</summary>
    public bool AllDay { get; set; }

    /// <summary>Event start date/time in UTC.</summary>
    public DateTime StartDate { get; set; }

    /// <summary>Event end date/time in UTC.</summary>
    public DateTime EndDate { get; set; }

    /// <summary>IANA time-zone ID used for the start date display.</summary>
    public string? StartDateTimeZoneIanaId { get; set; }

    /// <summary>IANA time-zone ID used for the end date display.</summary>
    public string? EndDateTimeZoneIanaId { get; set; }

    /// <summary>Event location.</summary>
    public string? Location { get; set; }

    /// <summary>Availability status (e.g. "Busy", "Free").</summary>
    public string? Status { get; set; }

    /// <summary>Optional recurrence rule ID (null for non-repeating events).</summary>
    public long? RecurrenceId { get; set; }

    /// <summary>UTC creation timestamp.</summary>
    public DateTime Created { get; set; }

    /// <summary>UTC last-updated timestamp.</summary>
    public DateTime Updated { get; set; }
}
