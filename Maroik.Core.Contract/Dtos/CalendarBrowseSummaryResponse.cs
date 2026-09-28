// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Data transfer object for one row of the "browse calendars of interest" list: a calendar shared
/// with registered users, alongside whether the current user is already subscribed to it.
/// </summary>
public class CalendarBrowseSummaryResponse
{
    /// <summary>Calendar ID.</summary>
    public long Id { get; set; }

    /// <summary>Calendar name.</summary>
    public string? Name { get; set; }

    /// <summary>True when the current user is already subscribed to this calendar.</summary>
    public bool Checked { get; set; }
}
