using Maroik.Core.Domain.Calendars;
namespace Maroik.Core.Domain.Tests.Calendars;

/// <summary>
/// Unit tests for <see cref="ReminderDeliveryPolicy"/>: whether a due reminder is e-mailed to its calendar's owner, or skipped
/// (with the reason the scheduler logs) — the owner is gone, deleted, locked, has not confirmed the address or is the public demo
/// account, or the reminder is over an hour late.
/// </summary>
public class ReminderDeliveryPolicyTests
{
    /// <summary>"Now".</summary>
    private static readonly DateTime _now = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);

    /// <summary>An account with the given state.</summary>
    private static ReminderRecipient Owner(string email = "owner@example.com", bool locked = false, bool confirmed = true, bool deleted = false) =>
        new(email, deleted, locked, confirmed);

    /// <summary>A confirmed, active owner's reminder that is due now, or under an hour late, is sent.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(59)]
    [InlineData(60)]
    public void AnActiveOwnersReminder_DueWithinTheHour_IsSent(int minutesLate)
    {
        Assert.Null(ReminderDeliveryPolicy.SkipReason(Owner(), "demo@maroik.com", _now.AddMinutes(-minutesLate), _now));
    }

    /// <summary>A reminder is skipped, with the reason, for each owner who must not get it, and when it is over an hour late.</summary>
    [Fact]
    public void AReminder_IsSkipped_WithItsReason()
    {
        Assert.Equal("the calendar's owner no longer exists", ReminderDeliveryPolicy.SkipReason(null, null, _now, _now));
        Assert.Equal("the owner's account is deleted", ReminderDeliveryPolicy.SkipReason(Owner(deleted: true), null, _now, _now));
        Assert.Equal("the owner's account is locked", ReminderDeliveryPolicy.SkipReason(Owner(locked: true), null, _now, _now));
        Assert.Equal("the owner has not confirmed the e-mail address", ReminderDeliveryPolicy.SkipReason(Owner(confirmed: false), null, _now, _now));
        Assert.Equal("the owner is the public demo account",
            ReminderDeliveryPolicy.SkipReason(Owner("demo@maroik.com"), "Demo@Maroik.com", _now, _now));
        Assert.Equal("it is more than an hour late", ReminderDeliveryPolicy.SkipReason(Owner(), null, _now.AddMinutes(-61), _now));
    }
}
