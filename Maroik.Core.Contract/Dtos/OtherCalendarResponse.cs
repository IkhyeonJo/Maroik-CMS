// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Data transfer object returned when reading a calendar subscription record.
/// Represents a user's subscription to another user's shared calendar.
/// </summary>
public class OtherCalendarResponse
{
    /// <summary>Email of the subscribing account.</summary>
    public string? AccountEmail { get; set; }

    /// <summary>ID of the subscribed calendar.</summary>
    public long CalendarId { get; set; }
}
