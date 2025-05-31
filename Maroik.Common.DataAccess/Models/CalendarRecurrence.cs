using System;
using System.Collections.Generic;

namespace Maroik.Common.DataAccess.Models;

/// <summary>
/// Maroik.WebSite 달력 반복
/// </summary>
public partial class CalendarRecurrence
{
    /// <summary>
    /// PK
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 반복 주기
    /// </summary>
    public string Frequency { get; set; }

    /// <summary>
    /// 반복 간격
    /// </summary>
    public long Interval { get; set; }

    /// <summary>
    /// 반복 요일
    /// </summary>
    public string DayOfWeek { get; set; }

    /// <summary>
    /// 매월 반복일 (1~31, 옵션)
    /// </summary>
    public long? DayOfMonth { get; set; }

    /// <summary>
    /// 매년 반복월 (1~12, 옵션)
    /// </summary>
    public long? MonthOfYear { get; set; }

    /// <summary>
    /// 반복 횟수 (옵션)
    /// </summary>
    public long? Count { get; set; }

    /// <summary>
    /// 반복 종료 날짜 (옵션)
    /// </summary>
    public DateTime? Until { get; set; }

    /// <summary>
    /// 생성일
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// 업데이트일
    /// </summary>
    public DateTime Updated { get; set; }

    public virtual ICollection<CalendarEvent> CalendarEvents { get; set; } = new List<CalendarEvent>();
}
