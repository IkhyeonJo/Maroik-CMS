using System;
using System.Collections.Generic;

namespace Maroik.Common.DataAccess.Models;

/// <summary>
/// Maroik.WebSite 게시판 댓글
/// </summary>
public partial class BoardComment
{
    /// <summary>
    /// PK
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 게시물 Id
    /// </summary>
    public long BoardId { get; set; }

    /// <summary>
    /// 순서
    /// </summary>
    public long Order { get; set; }

    /// <summary>
    /// 계정 아바타 이미지 경로
    /// </summary>
    public string AvatarImagePath { get; set; }

    /// <summary>
    /// 작성자
    /// </summary>
    public string Writer { get; set; }

    /// <summary>
    /// 내용
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// 생성일
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// 삭제 여부
    /// </summary>
    public bool Deleted { get; set; }

    public virtual Board Board { get; set; }
}
