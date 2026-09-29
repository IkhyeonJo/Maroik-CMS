// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Data transfer object returned when reading a file attached to a calendar event.
/// </summary>
public class CalendarEventAttachedFileDto
{
    /// <summary>Attached file ID (primary key).</summary>
    public long Id { get; set; }

    /// <summary>ID of the parent calendar event.</summary>
    public long CalendarEventId { get; set; }

    /// <summary>File size in bytes.</summary>
    public long Size { get; set; }

    /// <summary>Original file name.</summary>
    public string? Name { get; set; }

    /// <summary>File extension (e.g. ".zip").</summary>
    public string? Extension { get; set; }

    /// <summary>Server-side storage path of the file.</summary>
    public string? Path { get; set; }
}
