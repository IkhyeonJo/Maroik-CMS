using System;
using System.Collections.Generic;

namespace Maroik.Core.PostgreSQL.Models;

/// <summary>
/// Calendar
/// </summary>
public partial class Calendar
{
    /// <summary>
    /// PK
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// AccountEmail (ID)
    /// </summary>
    public string AccountEmail { get; set; } = null!;

    /// <summary>
    /// Name
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// IANA TimeZone ID
    /// </summary>
    public string TimeZoneIanaId { get; set; } = null!;

    /// <summary>
    /// HtmlColorCode
    /// </summary>
    public string HtmlColorCode { get; set; } = null!;

    /// <summary>
    /// Created
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// Updated
    /// </summary>
    public DateTime Updated { get; set; }

    /// <summary>The account that owns this calendar.</summary>
    public virtual Account AccountEmailNavigation { get; set; } = null!;

    /// <summary>Events in this calendar.</summary>
    public virtual ICollection<CalendarEvent> CalendarEvents { get; set; } = new List<CalendarEvent>();

    /// <summary>This calendar's sharing flags (one-to-one), or null when no row has been created yet.</summary>
    public virtual CalendarShared? CalendarShared { get; set; }

    /// <summary>Other accounts' subscriptions to this calendar.</summary>
    public virtual ICollection<OtherCalendar> OtherCalendars { get; set; } = new List<OtherCalendar>();
}
