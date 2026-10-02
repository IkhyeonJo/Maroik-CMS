// ReSharper disable PropertyCanBeMadeInitOnly.Global
namespace Maroik.Website.Models.ViewModels.Calendar;

/// <summary>
/// The file attached to a calendar event as the event detail describes it to the client: what the link shows, nothing more. The
/// storage path stays on the server — the file is fetched through <c>CalendarController.DownloadCalendarEventAttachedFile</c>.
/// </summary>
public class CalendarEventAttachedFileOutputViewModel
{
    /// <summary>Original file name, without its extension.</summary>
    public string? Name { get; set; }

    /// <summary>File extension (e.g. ".zip").</summary>
    public string? Extension { get; set; }

    /// <summary>File size in bytes.</summary>
    public long Size { get; set; }
}
