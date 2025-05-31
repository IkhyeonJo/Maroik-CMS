using System;
using System.Collections.Generic;

namespace Maroik.Common.DataAccess.Models;

/// <summary>
/// Maroik.WebSite 달력
/// </summary>
public partial class Calendar
{
    /// <summary>
    /// PK
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 계정 이메일 (ID)
    /// </summary>
    public string AccountEmail { get; set; }

    /// <summary>
    /// 이름
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 설명
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// IANA TimeZone ID
    /// </summary>
    public string TimeZoneIanaId { get; set; }

    /// <summary>
    /// 대표색
    /// </summary>
    public string HtmlColorCode { get; set; }

    /// <summary>
    /// 생성일
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// 업데이트일
    /// </summary>
    public DateTime Updated { get; set; }

    public virtual Account AccountEmailNavigation { get; set; }

    public virtual ICollection<CalendarEvent> CalendarEvents { get; set; } = new List<CalendarEvent>();

    public virtual CalendarShared CalendarShared { get; set; }

    public virtual ICollection<Account> AccountEmails { get; set; } = new List<Account>();
    public virtual ICollection<OtherCalendar> OtherCalendars { get; set; } 
}
