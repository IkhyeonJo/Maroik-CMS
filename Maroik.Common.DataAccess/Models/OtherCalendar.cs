namespace Maroik.Common.DataAccess.Models;

/// <summary>
/// Maroik.WebSite 달력
/// </summary>
public partial class OtherCalendar
{
    /// <summary>
    /// 계정 이메일 (ID)
    /// </summary>
    public string AccountEmail { get; set; }

    /// <summary>
    /// 부모 Calendar Id
    /// </summary>
    public long CalendarId { get; set; }

    public virtual Account Account { get; set; }

    public virtual Calendar Calendar { get; set; }
}