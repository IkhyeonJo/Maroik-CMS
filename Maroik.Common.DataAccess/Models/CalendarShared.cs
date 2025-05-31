using System;
using System.Collections.Generic;

namespace Maroik.Common.DataAccess.Models;

/// <summary>
/// Maroik.WebSite 달력 공유
/// </summary>
public partial class CalendarShared
{
    /// <summary>
    /// 부모 Calendar Id
    /// </summary>
    public long CalendarId { get; set; }

    /// <summary>
    /// User 공유 여부
    /// </summary>
    public bool User { get; set; }

    /// <summary>
    /// Anonymous 공유 여부
    /// </summary>
    public bool Anonymous { get; set; }

    public virtual Calendar Calendar { get; set; }
}
