namespace Maroik.Core.Contract.Dtos;

/// <summary>What a calendar reminder e-mail shows (<c>IMailClient.GetCalendarReminderBody</c>); the values are plain text.</summary>
/// <param name="Title">The event's title.</param>
/// <param name="When">The event's time, already formatted in the owner's time zone.</param>
/// <param name="Location">The event's place, or null / blank for none.</param>
/// <param name="CalendarName">The name of the event's calendar.</param>
/// <param name="CalendarPath">The app-relative path of the owner's calendar page (e.g. <c>Calendar/UserIndex</c>).</param>
public sealed record CalendarReminderMail(string Title, string When, string? Location, string CalendarName, string CalendarPath);
