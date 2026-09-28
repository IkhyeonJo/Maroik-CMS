using System.Text.Json;
using System.Text.Json.Serialization;
using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Calendar;
using Maroik.Core.Domain.Localization;
using Maroik.Website.Extensions;
using Maroik.Website.Models.ViewModels.Calendar;

namespace Maroik.Website.Mappings;

/// <summary>
/// Maps between calendar DTOs and view models in both directions:
/// Response → OutputViewModel (display) and InputViewModel → Request (form submission).
/// Centralizes UTC↔local timezone conversion and reminder serialization/deserialization.
/// </summary>
public static class CalendarViewModelMapper
{
    extension(CalendarEventResponse calendarEvent)
    {
        /// <summary>
        /// Maps a calendar event DTO to a <see cref="CalendarEventOutputViewModel"/>.
        /// Converts UTC dates to the viewer's local timezone and populates all display strings.
        /// </summary>
        /// <param name="userTimeZoneIanaId">Viewer's IANA timezone ID (used for all-day events and as fallback).</param>
        /// <param name="htmlColorCode">Parent calendar color code for chip/badge styling.</param>
        /// <param name="calendarType">Optional "My" / "Other" label; omit for single-owner views.</param>
        /// <param name="forGrid">
        /// When <see langword="true"/> (default), sets <c>StartDate</c> / <c>EndDate</c> with the
        /// FullCalendar-compatible exclusive-end convention (EndDate = midnight of the day after local end).
        /// Pass <see langword="false"/> for detail / edit views that only need the display strings.
        /// </param>
        public CalendarEventOutputViewModel ToDisplayViewModel(
            string userTimeZoneIanaId,
            string? htmlColorCode,
            string? calendarType = null,
            bool forGrid = true)
        {
            CalendarEventOutputViewModel item = new()
            {
                Id = calendarEvent.Id,
                CalendarId = calendarEvent.CalendarId,
                Title = calendarEvent.Title,
                AllDay = calendarEvent.AllDay,
                HtmlColorCode = htmlColorCode,
                CalendarType = calendarType
            };

            if (calendarEvent.AllDay)
            {
                // An all-day event is stored as midnight UTC standing in for a bare calendar date
                // (see ToCalendarEventRequest) — it carries no time-of-day, so it must NOT be
                // time-zone converted. Converting it shifts the date a day earlier for any viewer
                // west of UTC, and because the edit form is pre-filled from these values every
                // open/save cycle walks the event one more day back.
                DateTime start = DateTime.SpecifyKind(calendarEvent.StartDate, DateTimeKind.Unspecified);
                DateTime end = DateTime.SpecifyKind(calendarEvent.EndDate, DateTimeKind.Unspecified);

                item.StartDateTimeZoneIanaId = userTimeZoneIanaId;
                item.EndDateTimeZoneIanaId = userTimeZoneIanaId;
                item.DisplayStartDate = start.ToString("yyyy-MM-dd");
                item.DisplayEndDate = end.ToString("yyyy-MM-dd");
                item.DisplayStartDateTimeZone = "";
                item.DisplayEndDateTimeZone = "";

                if (!forGrid)
                {
                    return item;
                }

                // FullCalendar treats an all-day event's end as exclusive, so the grid end is
                // midnight of the day after the stored (inclusive) end date. This convention is
                // only correct for all-day events.
                item.StartDate = start.Date;
                item.EndDate = end.Date.AddDays(1);
            }
            else
            {
                string startTzId = calendarEvent.StartDateTimeZoneIanaId ?? userTimeZoneIanaId;
                string endTzId = calendarEvent.EndDateTimeZoneIanaId ?? userTimeZoneIanaId;

                DateTime localStart = calendarEvent.StartDate.ConvertTimeByTimeZoneIanaId(startTzId);
                DateTime localEnd = calendarEvent.EndDate.ConvertTimeByTimeZoneIanaId(endTzId);

                item.StartDateTimeZoneIanaId = startTzId;
                item.EndDateTimeZoneIanaId = endTzId;
                item.DisplayStartDate = localStart.ToString("yyyy-MM-dd HH:mm");
                item.DisplayEndDate = localEnd.ToString("yyyy-MM-dd HH:mm");
                item.DisplayStartDateTimeZone = startTzId.ToTimeZoneStandardName();
                item.DisplayEndDateTimeZone = endTzId.ToTimeZoneStandardName();

                if (!forGrid)
                {
                    return item;
                }

                // A timed event keeps its real local start and end instants. The
                // exclusive-end-at-next-midnight rule above is an all-day convention only — applied
                // here it rendered every timed event as a block running to the following midnight.
                item.StartDate = localStart;
                item.EndDate = localEnd;
            }

            return item;
        }
    }

    extension(IEnumerable<CalendarEventReminderDto> reminders)
    {
        /// <summary>
        /// Serializes reminder response objects to the JSON array the client edit-form expects.
        /// </summary>
        public string ToSerializedReminders()
            => JsonSerialization.ToClientJson(reminders.Select(x => new CalendarReminderDto
            {
                Method = x.Method ?? "",
                MinutesBeforeEvent = x.MinutesBeforeEvent,
                HoursBeforeEvent = x.HoursBeforeEvent,
                DaysBeforeEvent = x.DaysBeforeEvent,
                WeeksBeforeEvent = x.WeeksBeforeEvent,
                TimesBeforeEvent = x.TimesBeforeEvent
            }));
    }

    extension(CalendarOutputViewModel vm)
    {
        /// <summary>
        /// Fills the time-grid fields on the view model for the viewer's current local time.
        /// </summary>
        /// <exception cref="FormatException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public void PopulateTimeData(string timeZoneIanaId)
        {
            DateTime ct = DateTime.UtcNow.ConvertTimeByTimeZoneIanaId(timeZoneIanaId);
            vm.CurrentDate = ct.ToString("yyyy-MM-dd");
            vm.CurrentHour = ct.Hour;
            vm.CurrentMinute = ct.Minute;
            vm.CurrentSecond = ct.Second;
            const int step = CalendarReminderPolicy.ReminderTimeOfDayStepMinutes;
            List<string> ti = [];
            for (int h = 0; h < 24; h++)
                for (int m = 0; m < 60; m += step)
                    ti.Add($"{h:D2}:{m:D2}");
            vm.TimeIntervals = ti;
            int nm = (int)Math.Round(ct.Minute / (double)step) * step;
            if (nm == 60) { ct = ct.AddHours(1); nm = 0; }
            vm.CurrentInterval = $"{ct.Hour:D2}:{nm:D2}";
        }
    }

    extension(CalendarEventInputViewModel vm)
    {
        /// <summary>
        /// Builds a <see cref="CalendarEventRequest"/> from the posted form view model.
        /// All-day events are stored as UTC midnight; timed events are converted from the
        /// user-supplied IANA timezone to UTC.
        /// </summary>
        public CalendarEventRequest ToCalendarEventRequest()
        {
            CalendarEventRequest req = new() { Id = vm.Id };

            if (vm.AllDay)
            {
                string[] d = vm.StartDate!.Split('-');
                req.StartDate = new DateTime(int.Parse(d[0]), int.Parse(d[1]), int.Parse(d[2]), 0, 0, 0, DateTimeKind.Utc);
                d = vm.EndDate!.Split('-');
                req.EndDate = new DateTime(int.Parse(d[0]), int.Parse(d[1]), int.Parse(d[2]), 0, 0, 0, DateTimeKind.Utc);
                req.StartDateTimeZoneIanaId = null;
                req.EndDateTimeZoneIanaId = null;
            }
            else
            {
                req.StartDate = ParseLocalDateTime(vm.StartDate!).ConvertToUtcByTimeZoneIanaId(vm.StartDateTimeZoneIanaId!);
                req.EndDate = ParseLocalDateTime(vm.EndDate!).ConvertToUtcByTimeZoneIanaId(vm.EndDateTimeZoneIanaId!);
                req.StartDateTimeZoneIanaId = vm.StartDateTimeZoneIanaId;
                req.EndDateTimeZoneIanaId = vm.EndDateTimeZoneIanaId;
            }

            req.CalendarId = vm.CalendarId;
            req.Title = vm.Title;
            req.Description = vm.Description;
            req.AllDay = vm.AllDay;
            req.Location = vm.Location;
            req.Status = vm.Status;
            return req;
        }
    }

    extension(string? serialized)
    {
        /// <summary>
        /// Deserializes the JSON reminder list from the form submission into a strongly-typed list.
        /// Returns an empty list when the source string is null or empty.
        /// </summary>
        public List<CalendarReminderDto> ToReminderInfoList()
        {
            List<ReminderFormItem> raw = string.IsNullOrEmpty(serialized)
                ? []
                : JsonSerializer.Deserialize<List<ReminderFormItem>>(serialized, _reminderFormOptions) ?? [];

            return [.. raw.Select(r => new CalendarReminderDto
            {
                Method = r.Method ?? "",
                MinutesBeforeEvent = r.MinutesBeforeEvent,
                HoursBeforeEvent = r.HoursBeforeEvent,
                DaysBeforeEvent = r.DaysBeforeEvent,
                WeeksBeforeEvent = r.WeeksBeforeEvent,
                TimesBeforeEvent = ParseTimeOfDay(r.TimesBeforeEvent)
            })];
        }
    }

    /// <summary>
    /// Deserialization options for the reminder rows posted by the calendar edit form. The client
    /// sends verbatim PascalCase keys; a lead-time number may arrive as a JSON string, so reading
    /// numbers from strings stays permitted (the previous <c>Convert.ToInt32</c> path tolerated both).
    /// </summary>
    private static readonly JsonSerializerOptions _reminderFormOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    /// <summary>
    /// Parses an <c>"HH:mm"</c> time-of-day string; null/empty or anything not a valid time of day
    /// (including an hour or minute out of range) yields <see langword="null"/> rather than
    /// throwing, since this reads directly from client-supplied JSON.
    /// </summary>
    private static TimeOnly? ParseTimeOfDay(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        return TimeOnly.TryParse(value, out TimeOnly result) ? result : null;
    }

    /// <summary>Wire shape of one reminder row in the calendar edit form's hidden JSON field.</summary>
    // ReSharper disable once ClassNeverInstantiated.Local
    private sealed class ReminderFormItem(string? method, int? weeksBeforeEvent, int? daysBeforeEvent, 
        int? hoursBeforeEvent, int? minutesBeforeEvent, string? timesBeforeEvent)
    {
        /// <summary>Delivery method as posted by the form (validated later by the domain reminder policy).</summary>
        public string? Method { get; } = method;
        /// <summary>Minutes before the event to send the reminder.</summary>
        public int? MinutesBeforeEvent { get; } = minutesBeforeEvent;
        /// <summary>Hours before the event to send the reminder.</summary>
        public int? HoursBeforeEvent { get; } = hoursBeforeEvent;
        /// <summary>Days before the event to send the reminder.</summary>
        public int? DaysBeforeEvent { get; } = daysBeforeEvent;
        /// <summary>Weeks before the event to send the reminder.</summary>
        public int? WeeksBeforeEvent { get; } = weeksBeforeEvent;
        /// <summary>Time of day (text, e.g. <c>HH:mm</c>) the reminder fires at on the target day; unparsable values become <see langword="null"/>.</summary>
        public string? TimesBeforeEvent { get; } = timesBeforeEvent;
    }

    /// <summary>Parses a "yyyy-MM-dd HH:mm" string into an unspecified-kind local <see cref="DateTime"/>.</summary>
    private static DateTime ParseLocalDateTime(string dateTimeStr)
    {
        string[] parts = dateTimeStr.Split(' ');
        string[] dateParts = parts[0].Split('-');
        string[] timeParts = parts[1].Split(':');
        return new DateTime(
            int.Parse(dateParts[0]), int.Parse(dateParts[1]), int.Parse(dateParts[2]),
            int.Parse(timeParts[0]), int.Parse(timeParts[1]), 0, DateTimeKind.Unspecified);
    }
}
