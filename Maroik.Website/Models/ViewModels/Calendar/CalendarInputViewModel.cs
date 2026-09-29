// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable PropertyCanBeMadeInitOnly.Global
using Maroik.Core.Contract.Dtos;
    
namespace Maroik.Website.Models.ViewModels.Calendar;

/// <summary>
/// JSON request body of the calendar endpoints: <c>CreateCalendar</c> / <c>UpdateCalendar</c> /
/// <c>DeleteCalendar</c> act on the first entry of <see cref="Calendars"/>, and <c>GetCalendarEvents</c>
/// fetches the events of the calendars it lists.
/// </summary>
public class CalendarInputViewModel
{
    /// <summary>The calendar(s) the request is about, as posted by the client script.</summary>
    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Global
    public IEnumerable<CalendarRequest> Calendars { get; set; } = [];
}
