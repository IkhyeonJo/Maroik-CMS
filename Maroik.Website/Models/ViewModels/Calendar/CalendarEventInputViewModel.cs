// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable PropertyCanBeMadeInitOnly.Global
using System.ComponentModel.DataAnnotations;
using System.Globalization;
// ReSharper disable InvalidXmlDocComment

namespace Maroik.Website.Models.ViewModels.Calendar;

/// <summary>
/// Form model bound from the calendar event create / edit POST requests.
/// Dates are received as strings rather than <see cref="DateTime"/> values because the client submits
/// local wall-clock values together with separate time-zone fields; <c>CalendarViewModelMapper.ToCalendarEventRequest</c>
/// converts them to UTC (after <see cref="Validate"/> has checked their shape).
/// </summary>
public class CalendarEventInputViewModel : IValidatableObject
{
    /// <summary>Database row ID; 0 for a new event, positive for an edit.</summary>
    public int Id { get; set; }

    /// <summary>The owning calendar's database ID.</summary>
    public int CalendarId { get; set; }

    /// <summary>Title / headline of the calendar event.</summary>
    public string? Title { get; set; }

    /// <summary>
    /// When <see langword="true"/> the event spans full days and has no specific start/end time.
    /// </summary>
    public bool AllDay { get; set; }

    /// <summary>Start as "yyyy-M-d H:m" wall-clock time in <see cref="StartDateTimeZoneIanaId"/>, or "yyyy-M-d" for an all-day event.</summary>
    public string? StartDate { get; set; }

    /// <summary>End as "yyyy-M-d H:m" wall-clock time in <see cref="EndDateTimeZoneIanaId"/>, or "yyyy-M-d" for an all-day event.</summary>
    public string? EndDate { get; set; }

    /// <summary>IANA time-zone ID that applies to <see cref="StartDate"/> (e.g. "Asia/Seoul").</summary>
    public string? StartDateTimeZoneIanaId { get; set; }

    /// <summary>IANA time-zone ID that applies to <see cref="EndDate"/>.</summary>
    public string? EndDateTimeZoneIanaId { get; set; }

    /// <summary>Optional physical or virtual location for the event.</summary>
    public string? Location { get; set; }

    /// <summary>Optional longer description / body text for the event.</summary>
    public string? Description { get; set; }

    /// <summary>Optional file attachment uploaded with the event (max one file per event).</summary>
    public IFormFile? CalendarEventUploadedFile { get; set; }

    /// <summary>
    /// Availability status shown to subscribers ("Busy" or "Free"; blank means the "Busy" default).
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// JSON-serialized list of <c>CalendarReminderDto</c> objects representing the reminders
    /// configured for this event. Serialized on the client and deserialized by the controller
    /// (<c>ToReminderInfoList</c>) before it reaches the service layer.
    /// </summary>
    public string? SerializedCalendarReminders { get; set; }

    /// <summary>
    /// Ensures the date/timezone fields <c>CalendarViewModelMapper.ToCalendarEventRequest</c>
    /// parses are present and well-formed before the mapper ever runs, so a missing or malformed
    /// value fails cleanly as a validation error instead of throwing
    /// (<see cref="IndexOutOfRangeException"/>/<see cref="FormatException"/>/
    /// <see cref="ArgumentNullException"/>) from inside the mapper.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AllDay)
        {
            if (!IsValidDateOnly(StartDate))
                yield return new ValidationResult("Please enter a valid StartDate", [nameof(StartDate)]);
            if (!IsValidDateOnly(EndDate))
                yield return new ValidationResult("Please enter a valid EndDate", [nameof(EndDate)]);
        }
        else
        {
            if (!IsValidLocalDateTime(StartDate))
                yield return new ValidationResult("Please enter a valid StartDate", [nameof(StartDate)]);
            if (!IsValidLocalDateTime(EndDate))
                yield return new ValidationResult("Please enter a valid EndDate", [nameof(EndDate)]);
            if (string.IsNullOrWhiteSpace(StartDateTimeZoneIanaId))
                yield return new ValidationResult("Please enter StartDateTimeZoneIanaId", [nameof(StartDateTimeZoneIanaId)]);
            if (string.IsNullOrWhiteSpace(EndDateTimeZoneIanaId))
                yield return new ValidationResult("Please enter EndDateTimeZoneIanaId", [nameof(EndDateTimeZoneIanaId)]);
        }
    }

    /// <summary>Matches the "yyyy-M-d" shape <c>ToCalendarEventRequest</c> splits an all-day date on.</summary>
    private static bool IsValidDateOnly(string? value) =>
        DateTime.TryParseExact(value, "yyyy-M-d", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    /// <summary>Matches the "yyyy-M-d H:m" shape <c>ParseLocalDateTime</c> splits a timed date on.</summary>
    private static bool IsValidLocalDateTime(string? value) =>
        DateTime.TryParseExact(value, "yyyy-M-d H:m", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}
