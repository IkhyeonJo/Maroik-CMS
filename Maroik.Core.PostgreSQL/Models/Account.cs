using System;
using System.Collections.Generic;

namespace Maroik.Core.PostgreSQL.Models;

/// <summary>
/// Account
/// </summary>
public partial class Account
{
    /// <summary>
    /// Email (ID)
    /// </summary>
    public string Email { get; set; } = null!;

    /// <summary>
    /// HashedPassword
    /// </summary>
    public string HashedPassword { get; set; } = null!;

    /// <summary>
    /// Nickname
    /// </summary>
    public string Nickname { get; set; } = null!;

    /// <summary>
    /// AvatarImagePath
    /// </summary>
    public string AvatarImagePath { get; set; } = null!;

    /// <summary>
    /// Role (Admin or User)
    /// </summary>
    public string Role { get; set; } = null!;

    /// <summary>
    /// IANA TimeZone ID
    /// </summary>
    public string TimeZoneIanaId { get; set; } = null!;

    /// <summary>
    /// Default monetary unit (KRW, USD, ETC)
    /// </summary>
    public string? DefaultMonetaryUnit { get; set; }

    /// <summary>
    /// Locked
    /// </summary>
    public bool Locked { get; set; }

    /// <summary>
    /// LoginAttempt
    /// </summary>
    public long LoginAttempt { get; set; }

    /// <summary>
    /// EmailConfirmed
    /// </summary>
    public bool EmailConfirmed { get; set; }

    /// <summary>
    /// AgreedServiceTerms
    /// </summary>
    public bool AgreedServiceTerms { get; set; }

    /// <summary>
    /// RegistrationToken
    /// </summary>
    public string? RegistrationToken { get; set; }

    /// <summary>
    /// ResetPasswordToken
    /// </summary>
    public string? ResetPasswordToken { get; set; }

    /// <summary>
    /// Created
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// Updated
    /// </summary>
    public DateTime Updated { get; set; }

    /// <summary>
    /// Message
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Deleted
    /// </summary>
    public bool Deleted { get; set; }

    /// <summary>
    /// SecurityStamp
    /// </summary>
    public string SecurityStamp { get; set; } = null!;

    /// <summary>
    /// MustChangePassword
    /// </summary>
    public bool MustChangePassword { get; set; }

    /// <summary>Assets owned by this account (Asset.AccountEmail).</summary>
    public virtual ICollection<Asset> Assets { get; set; } = new List<Asset>();

    /// <summary>Calendars owned by this account (Calendar.AccountEmail).</summary>
    public virtual ICollection<Calendar> Calendars { get; set; } = new List<Calendar>();

    /// <summary>This account's subscriptions to other accounts' calendars (OtherCalendar.AccountEmail).</summary>
    public virtual ICollection<OtherCalendar> OtherCalendars { get; set; } = new List<OtherCalendar>();
}
