using System;
using System.Collections.Generic;

namespace Maroik.Core.PostgreSQL.Models;

/// <summary>
/// CalendarEvent
/// </summary>
public partial class CalendarEvent
{
    /// <summary>
    /// PK
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Parent Calendar Id
    /// </summary>
    public long CalendarId { get; set; }

    /// <summary>
    /// Title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// AllDay
    /// </summary>
    public bool AllDay { get; set; }

    /// <summary>
    /// StartDate
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// EndDate
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// StartDateTimeZone (IANA TimeZone ID)
    /// </summary>
    public string? StartDateTimeZoneIanaId { get; set; }

    /// <summary>
    /// EndDateTimeZone (IANA TimeZone ID)
    /// </summary>
    public string? EndDateTimeZoneIanaId { get; set; }

    /// <summary>
    /// Location
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Status
    /// </summary>
    public string Status { get; set; } = null!;

    /// <summary>
    /// Recurrence ID (Option)
    /// </summary>
    public long? RecurrenceId { get; set; }

    /// <summary>
    /// Created
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// Updated
    /// </summary>
    public DateTime Updated { get; set; }

    /// <summary>The calendar this event belongs to.</summary>
    public virtual Calendar Calendar { get; set; } = null!;

    /// <summary>Files attached to this event.</summary>
    public virtual ICollection<CalendarEventAttachedFile> CalendarEventAttachedFiles { get; set; } = new List<CalendarEventAttachedFile>();

    /// <summary>Reminders configured for this event.</summary>
    public virtual ICollection<CalendarEventReminder> CalendarEventReminders { get; set; } = new List<CalendarEventReminder>();

    /// <summary>The recurrence rule referenced by <see cref="RecurrenceId"/>, if any.</summary>
    public virtual CalendarRecurrence? Recurrence { get; set; }
}
