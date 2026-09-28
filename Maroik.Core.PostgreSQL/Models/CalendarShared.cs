using System;
using System.Collections.Generic;

namespace Maroik.Core.PostgreSQL.Models;

/// <summary>
/// CalendarShared
/// </summary>
public partial class CalendarShared
{
    /// <summary>
    /// Parent Calendar Id
    /// </summary>
    public long CalendarId { get; set; }

    /// <summary>
    /// Is user shared
    /// </summary>
    public bool User { get; set; }

    /// <summary>
    /// Is anonymous shared
    /// </summary>
    public bool Anonymous { get; set; }

    public virtual Calendar Calendar { get; set; } = null!;
}
