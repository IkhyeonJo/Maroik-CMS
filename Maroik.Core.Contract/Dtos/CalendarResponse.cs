// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Data transfer object returned when reading a calendar.
/// </summary>
public class CalendarResponse
{
    /// <summary>Calendar ID (primary key).</summary>
    public long Id { get; set; }

    /// <summary>Email of the owning account.</summary>
    public string? AccountEmail { get; set; }

    /// <summary>Calendar display name.</summary>
    public string? Name { get; set; }

    /// <summary>Optional description.</summary>
    public string? Description { get; set; }

    /// <summary>Default IANA time-zone ID for events in this calendar.</summary>
    public string? TimeZoneIanaId { get; set; }

    /// <summary>HTML color code (e.g. "#FF5733") used to render calendar events.</summary>
    public string? HtmlColorCode { get; set; }

    /// <summary>UTC creation timestamp.</summary>
    public DateTime Created { get; set; }

    /// <summary>UTC last-updated timestamp.</summary>
    public DateTime Updated { get; set; }
}
