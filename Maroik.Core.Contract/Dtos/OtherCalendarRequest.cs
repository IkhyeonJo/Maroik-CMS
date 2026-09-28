// ReSharper disable PropertyCanBeMadeInitOnly.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Data transfer object used when a user subscribes to or unsubscribes from another user's shared
/// calendar. The subscriber is always the caller — the service takes it from the session — so only
/// the target calendar id travels in the request.
/// </summary>
public class OtherCalendarRequest
{
    /// <summary>ID of the calendar being subscribed to.</summary>
    public long CalendarId { get; set; }
}
