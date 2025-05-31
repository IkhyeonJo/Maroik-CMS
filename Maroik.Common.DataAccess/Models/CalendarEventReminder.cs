using System;
using System.Collections.Generic;

namespace Maroik.Common.DataAccess.Models;

/// <summary>
/// Maroik.WebSite 달력 이벤트 알림
/// </summary>
public partial class CalendarEventReminder
{
    /// <summary>
    /// PK
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 부모 Calendar Event Id
    /// </summary>
    public long CalendarEventId { get; set; }

    /// <summary>
    /// 알림 방법
    /// </summary>
    public string Method { get; set; }

    /// <summary>
    /// 이벤트 발생 전 알림 시간 (분 단위)
    /// </summary>
    public long? MinutesBeforeEvent { get; set; }

    /// <summary>
    /// 이벤트 발생 전 알림 시간 (시간 단위)
    /// </summary>
    public long? HoursBeforeEvent { get; set; }

    /// <summary>
    /// 이벤트 발생 전 알림 시간 (일 단위)
    /// </summary>
    public long? DaysBeforeEvent { get; set; }

    /// <summary>
    /// 이벤트 발생 전 알림 시간 (주 단위)
    /// </summary>
    public long? WeeksBeforeEvent { get; set; }

    /// <summary>
    /// 이벤트 발생 전 알림 시간 (시간분 단위)
    /// </summary>
    public TimeOnly? TimesBeforeEvent { get; set; }

    public virtual CalendarEvent CalendarEvent { get; set; }
}
