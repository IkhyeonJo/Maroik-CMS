// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Data transfer object used to create or update a calendar.
/// A calendar groups multiple events and has its own color and time zone.
/// </summary>
public class CalendarRequest
{
    /// <summary>Calendar ID (auto-incremented primary key; 0 for new calendars).</summary>
    public long Id { get; set; }

    /// <summary>Email of the account that owns this calendar (foreign key).</summary>
    public string? AccountEmail { get; set; }

    /// <summary>Calendar display name (e.g. "Work", "Personal").</summary>
    public string? Name { get; set; }

    /// <summary>Optional description of the calendar's purpose.</summary>
    public string? Description { get; set; }

    /// <summary>Default IANA time-zone ID for events in this calendar (e.g. "Asia/Seoul").</summary>
    public string? TimeZoneIanaId { get; set; }

    /// <summary>HTML color code used to visually distinguish this calendar (e.g. "#FF5733").</summary>
    public string? HtmlColorCode { get; set; }
}
