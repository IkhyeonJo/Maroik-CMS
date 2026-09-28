// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Data transfer object for one row of the Management &gt; Calendar "shared calendars" grid: an
/// owned calendar's name alongside its current user/guest sharing flags.
/// </summary>
public class CalendarSharedSummaryResponse
{
    /// <summary>Calendar ID.</summary>
    public long Id { get; set; }

    /// <summary>Calendar name.</summary>
    public string? Name { get; set; }

    /// <summary>True when registered users can view this calendar.</summary>
    public bool User { get; set; }

    /// <summary>True when anonymous visitors can view this calendar.</summary>
    public bool Guest { get; set; }
}
