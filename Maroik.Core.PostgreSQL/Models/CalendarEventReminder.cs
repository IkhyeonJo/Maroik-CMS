using System;
using System.Collections.Generic;

namespace Maroik.Core.PostgreSQL.Models;

/// <summary>
/// CalendarEventReminder
/// </summary>
public partial class CalendarEventReminder
{
    /// <summary>
    /// PK
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Parent Calendar Event Id
    /// </summary>
    public long CalendarEventId { get; set; }

    /// <summary>
    /// Method
    /// </summary>
    public string Method { get; set; } = null!;

    /// <summary>
    /// MinutesBeforeEvent
    /// </summary>
    public long? MinutesBeforeEvent { get; set; }

    /// <summary>
    /// HoursBeforeEvent
    /// </summary>
    public long? HoursBeforeEvent { get; set; }

    /// <summary>
    /// DaysBeforeEvent
    /// </summary>
    public long? DaysBeforeEvent { get; set; }

    /// <summary>
    /// WeeksBeforeEvent
    /// </summary>
    public long? WeeksBeforeEvent { get; set; }

    /// <summary>
    /// TimesBeforeEvent
    /// </summary>
    public TimeOnly? TimesBeforeEvent { get; set; }

    public virtual CalendarEvent CalendarEvent { get; set; } = null!;
}
