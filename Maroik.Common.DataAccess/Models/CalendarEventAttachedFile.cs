using System;
using System.Collections.Generic;

namespace Maroik.Common.DataAccess.Models;

/// <summary>
/// Maroik.WebSite 달력 이벤트 첨부파일
/// </summary>
public partial class CalendarEventAttachedFile
{
    /// <summary>
    /// PK
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 부모 CalendarEvent Id
    /// </summary>
    public long CalendarEventId { get; set; }

    /// <summary>
    /// 크기 (Byte)
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// 이름
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 확장자
    /// </summary>
    public string Extension { get; set; }

    /// <summary>
    /// 경로
    /// </summary>
    public string Path { get; set; }

    public virtual CalendarEvent CalendarEvent { get; set; }
}
