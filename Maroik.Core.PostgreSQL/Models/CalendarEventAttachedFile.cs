using System;
using System.Collections.Generic;

namespace Maroik.Core.PostgreSQL.Models;

/// <summary>
/// CalendarEventAttachedFile
/// </summary>
public partial class CalendarEventAttachedFile
{
    /// <summary>
    /// PK
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Parent CalendarEvent Id
    /// </summary>
    public long CalendarEventId { get; set; }

    /// <summary>
    /// Size (Byte)
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Name
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Extension
    /// </summary>
    public string? Extension { get; set; }

    /// <summary>
    /// Path
    /// </summary>
    public string Path { get; set; } = null!;

    /// <summary>The event this file is attached to.</summary>
    public virtual CalendarEvent CalendarEvent { get; set; } = null!;
}
