using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Website.Mappings;
using Maroik.Website.Models.ViewModels.Calendar;

namespace Maroik.Website.Extensions;

/// <summary>
/// Presentation-layer extensions on <see cref="ICalendarService"/> that combine
/// service data-fetching with <see cref="CalendarViewModelMapper"/> mapping,
/// so controllers receive ready-to-render view model lists without orchestrating the loop.
/// </summary>
public static class CalendarEventViewModelExtensions
{
    extension(ICalendarService calendarService)
    {
        /// <summary>
        /// Fetches all events for each calendar in <paramref name="calendars"/> and returns
        /// them as grid-ready view models with timezone conversion applied.
        /// </summary>
        /// <param name="calendars">Calendars whose events should be loaded.</param>
        /// <param name="userTimeZoneIanaId">Viewer's IANA timezone ID for date conversion.</param>
        /// <param name="calendarType">Optional "My" / "Other" label passed through to each item.</param>
        /// <param name="ct">Cancellation token.</param>
        public async Task<List<CalendarEventOutputViewModel>> GetCalendarEventViewModelsAsync(
            IEnumerable<CalendarResponse> calendars,
            string userTimeZoneIanaId,
            string? calendarType,
            CancellationToken ct)
        {
            List<CalendarResponse> calendarList = [.. calendars];

            // Fetch every calendar's events in a single query instead of one round trip per calendar.
            IReadOnlyDictionary<long, List<CalendarEventResponse>> eventsByCalendarId =
                await calendarService.GetCalendarEventsAsync(calendarList.Select(c => c.Id), ct);

            return
            [
                .. from calendar in calendarList
                   from evt in eventsByCalendarId.GetValueOrDefault(calendar.Id, [])
                   select evt.ToDisplayViewModel(userTimeZoneIanaId, calendar.HtmlColorCode, calendarType)
            ];
        }

        /// <summary>
        /// Loads the given calendar event and, if it belongs to one of <paramref name="calendars"/>,
        /// returns it as a fully populated <see cref="CalendarEventOutputViewModel"/> (attachment,
        /// reminders, color). Returns <see langword="null"/> if the event doesn't exist or doesn't
        /// belong to any of the given calendars, so the controller can turn that into its own
        /// localized JSON error response.
        /// </summary>
        /// <param name="calendars">Calendars the caller is authorized to see.</param>
        /// <param name="userTimeZoneIanaId">Viewer's IANA timezone ID for date conversion.</param>
        /// <param name="calendarEventId">ID of the calendar event to load.</param>
        /// <param name="ct">Cancellation token.</param>
        public async Task<CalendarEventOutputViewModel?> GetCalendarEventDetailViewModelAsync(
            IEnumerable<CalendarResponse> calendars,
            string userTimeZoneIanaId,
            int calendarEventId,
            CancellationToken ct)
        {
            List<CalendarResponse> calendarList = [.. calendars];
            CalendarEventResponse? tempCalendarEvent = await calendarService.GetCalendarEventAsync(calendarEventId, ct);

            if (tempCalendarEvent is not { Id: > 0 })
                return null;

            CalendarResponse tempCalendar = calendarList.FirstOrDefault(x => x.Id == tempCalendarEvent.CalendarId) ?? new CalendarResponse();
            if (!(tempCalendar.Id > 0))
                return null;

            CalendarEventOutputViewModel calendarEvent = tempCalendarEvent.ToDisplayViewModel(userTimeZoneIanaId, null, forGrid: false);

            (calendarEvent.Description, _) = await calendarService.PrepareHtmlForDisplayAsync(tempCalendarEvent.Description ?? "", ct);
            calendarEvent.Location = tempCalendarEvent.Location;
            calendarEvent.Status = tempCalendarEvent.Status;
            calendarEvent.CalendarEventAttachedFile = await calendarService.GetCalendarEventAttachedFileAsync(tempCalendarEvent.Id, ct);

            calendarEvent.Calendars =
                [.. calendarList.Select(x => new CalendarResponse { Id = x.Id, Name = x.Name }).OrderBy(x => x.Name)];
            calendarEvent.SerializedCalendarReminders = (await calendarService.GetCalendarEventRemindersAsync(tempCalendarEvent.Id, ct)).ToSerializedReminders();
            calendarEvent.HtmlColorCode = calendarList.FirstOrDefault(x => x.Id == tempCalendarEvent.CalendarId)?.HtmlColorCode;

            return calendarEvent;
        }
    }
}
