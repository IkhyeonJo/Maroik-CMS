// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable PropertyCanBeMadeInitOnly.Global
using Maroik.Core.Contract.Dtos;

namespace Maroik.Website.Models.ViewModels.Calendar;

/// <summary>
/// Read-only view model for a calendar event as rendered in the calendar grid or event detail panel.
/// Extends the raw DTO with pre-formatted display strings, the calendar color, and the
/// Base64-encoded attachment so the view can embed the file without a separate download request.
/// </summary>
public class CalendarEventOutputViewModel
{
    /// <summary>Unique database ID of this calendar event.</summary>
    public long Id { get; set; }

    /// <summary>Database ID of the calendar this event belongs to.</summary>
    public long CalendarId { get; set; }

    /// <summary>Title / headline of the calendar event.</summary>
    public string? Title { get; set; }

    /// <summary>When <see langword="true"/> the event spans full days and has no specific start/end time.</summary>
    public bool AllDay { get; set; }

    /// <summary>
    /// Grid start: for a timed event, its start converted to <see cref="StartDateTimeZoneIanaId"/> (local
    /// wall-clock); for an all-day event, the stored start date. Left unset for detail / edit views.
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Grid end: for a timed event, its end converted to <see cref="EndDateTimeZoneIanaId"/>; for an
    /// all-day event, midnight after the stored (inclusive) end date (FullCalendar's exclusive end).
    /// Left unset for detail / edit views.
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>IANA time-zone ID used when the start date was entered (e.g. "Asia/Seoul").</summary>
    public string? StartDateTimeZoneIanaId { get; set; }

    /// <summary>IANA time-zone ID used when the end date was entered.</summary>
    public string? EndDateTimeZoneIanaId { get; set; }

    /// <summary>Optional physical or virtual location for the event.</summary>
    public string? Location { get; set; }

    /// <summary>Optional longer description / body text.</summary>
    public string? Description { get; set; }

    /// <summary>Metadata for the file attached to this event (null when no file is attached).</summary>
    public CalendarEventAttachedFileDto? CalendarEventAttachedFile { get; set; }

    /// <summary>All calendars available to the user, used to populate the calendar selector when editing this event.</summary>
    public List<CalendarResponse> Calendars { get; set; } = [];

    /// <summary>Availability status shown to subscribers ("Busy" or "Free" — see <c>CalendarEventStatuses</c>).</summary>
    public string? Status { get; set; }

    /// <summary>
    /// JSON-serialized reminders for this event; deserialized on the client side to
    /// pre-populate the reminder list when the user opens the edit form.
    /// </summary>
    public string? SerializedCalendarReminders { get; set; }

    /// <summary>HTML color code (e.g. "#3788d8") inherited from the parent calendar, used for event chip styling.</summary>
    public string? HtmlColorCode { get; set; }

    /// <summary>Human-readable start date-time string already formatted in the user's local time zone.</summary>
    public string? DisplayStartDate { get; set; }

    /// <summary>Human-readable end date-time string already formatted in the user's local time zone.</summary>
    public string? DisplayEndDate { get; set; }

    /// <summary>Display-friendly name of the start time zone (derived from <see cref="StartDateTimeZoneIanaId"/>).</summary>
    public string? DisplayStartDateTimeZone { get; set; }

    /// <summary>Display-friendly name of the end time zone (derived from <see cref="EndDateTimeZoneIanaId"/>).</summary>
    public string? DisplayEndDateTimeZone { get; set; }

    /// <summary>Ownership label of this event's calendar — a <c>CalendarTypes</c> constant ("My" or "Other").</summary>
    public string? CalendarType { get; set; }
}
