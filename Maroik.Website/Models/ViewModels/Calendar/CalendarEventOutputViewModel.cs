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

    /// <summary>UTC start date-time of the event (converted from the stored IANA-zone value).</summary>
    public DateTime StartDate { get; set; }

    /// <summary>UTC end date-time of the event.</summary>
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

    /// <summary>Base64-encoded bytes of the attached file, used for inline preview or download link generation.</summary>
    public string? CalendarEventAttachedFileBase64Data { get; set; }

    /// <summary>MIME type of the attached file (e.g. "image/png"), used for data-URI embedding.</summary>
    public string? CalendarEventAttachedFileContentType { get; set; }

    /// <summary>All calendars available to the user, used to populate the calendar selector when editing this event.</summary>
    public List<CalendarResponse> Calendars { get; set; } = [];

    /// <summary>Event status string (e.g. "Confirmed", "Tentative", "Canceled").</summary>
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

    /// <summary>String representation of the <c>CalendarType</c> enum (e.g. "My", "Other") for this event's calendar.</summary>
    public string? CalendarType { get; set; }
}
