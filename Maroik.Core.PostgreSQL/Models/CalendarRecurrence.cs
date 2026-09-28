using System;
using System.Collections.Generic;

namespace Maroik.Core.PostgreSQL.Models;

/// <summary>
/// CalendarRecurrence
/// </summary>
public partial class CalendarRecurrence
{
    /// <summary>
    /// PK
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Frequency
    /// </summary>
    public string Frequency { get; set; } = null!;

    /// <summary>
    /// Interval
    /// </summary>
    public long Interval { get; set; }

    /// <summary>
    /// DayOfWeek
    /// </summary>
    public string? DayOfWeek { get; set; }

    /// <summary>
    /// DayOfMonth
    /// </summary>
    public long? DayOfMonth { get; set; }

    /// <summary>
    /// MonthOfYear
    /// </summary>
    public long? MonthOfYear { get; set; }

    /// <summary>
    /// Count
    /// </summary>
    public long? Count { get; set; }

    /// <summary>
    /// Until
    /// </summary>
    public DateTime? Until { get; set; }

    /// <summary>
    /// Created
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// Updated
    /// </summary>
    public DateTime Updated { get; set; }

    public virtual ICollection<CalendarEvent> CalendarEvents { get; set; } = new List<CalendarEvent>();
}
