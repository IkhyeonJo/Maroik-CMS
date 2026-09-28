// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Data transfer object returned when reading the sharing settings of a calendar.
/// </summary>
public class CalendarSharedResponse
{
    /// <summary>Calendar ID (primary key).</summary>
    public long CalendarId { get; set; }

    /// <summary>True when registered users can view this calendar.</summary>
    public bool User { get; set; }

    /// <summary>True when anonymous visitors can view this calendar.</summary>
    public bool Anonymous { get; set; }
}
