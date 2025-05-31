using System;
using System.Collections.Generic;

namespace Maroik.Common.DataAccess.Models;

/// <summary>
/// Maroik.WebSite 계정
/// </summary>
public partial class Account
{
    /// <summary>
    /// 이메일 (ID)
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// 해시화 된 비밀번호
    /// </summary>
    public string HashedPassword { get; set; }

    /// <summary>
    /// 닉네임
    /// </summary>
    public string Nickname { get; set; }

    /// <summary>
    /// 아바타 이미지 경로
    /// </summary>
    public string AvatarImagePath { get; set; }

    /// <summary>
    /// 역할 (Admin 또는 User)
    /// </summary>
    public string Role { get; set; }

    /// <summary>
    /// IANA TimeZone ID
    /// </summary>
    public string TimeZoneIanaId { get; set; }

    /// <summary>
    /// 기본 화폐 단위 (KRW, USD, ETC)
    /// </summary>
    public string DefaultMonetaryUnit { get; set; }

    /// <summary>
    /// 잠금 여부
    /// </summary>
    public bool Locked { get; set; }

    /// <summary>
    /// 로그인 시도 횟수
    /// </summary>
    public byte LoginAttempt { get; set; }

    /// <summary>
    /// 이메일 확인 여부
    /// </summary>
    public bool EmailConfirmed { get; set; }

    /// <summary>
    /// 약관 동의 여부
    /// </summary>
    public bool AgreedServiceTerms { get; set; }

    /// <summary>
    /// 회원가입 인증 토큰
    /// </summary>
    public string RegistrationToken { get; set; }

    /// <summary>
    /// 비밀번호 찾기 인증 토큰
    /// </summary>
    public string ResetPasswordToken { get; set; }

    /// <summary>
    /// 생성일
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// 업데이트일
    /// </summary>
    public DateTime Updated { get; set; }

    /// <summary>
    /// 상태 메시지
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// 삭제 여부
    /// </summary>
    public bool Deleted { get; set; }

    public virtual ICollection<Asset> Assets { get; set; } = new List<Asset>();

    public virtual ICollection<Calendar> Calendars { get; set; } = new List<Calendar>();

    public virtual ICollection<Calendar> CalendarsNavigation { get; set; } = new List<Calendar>();
    public virtual ICollection<OtherCalendar> OtherCalendars { get; set; } 
}
