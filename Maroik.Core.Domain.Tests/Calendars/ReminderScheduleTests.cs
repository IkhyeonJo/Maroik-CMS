using Maroik.Core.Domain.Calendars;
namespace Maroik.Core.Domain.Tests.Calendars;

/// <summary>
/// Unit tests for <see cref="ReminderSchedule"/>: when a reminder fires. A timed event's reminder fires its lead time before the
/// event's start instant; an all-day event's fires on the day its lead time before the event's first day, at the reminder's time of
/// day (09:00 by default) in the calendar's time zone, with daylight-saving gaps and overlaps resolved.
/// </summary>
public class ReminderScheduleTests
{
    /// <summary>A timed event starting 2026-11-02 09:00 UTC.</summary>
    private static readonly DateTime _timedStart = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);

    /// <summary>A timed event's reminder fires its lead time before the start, whatever the calendar's zone.</summary>
    [Theory]
    [InlineData(15L, null, null, null, "2026-11-02T08:45:00")]
    [InlineData(0L, null, null, null, "2026-11-02T09:00:00")]
    [InlineData(null, 2L, null, null, "2026-11-02T07:00:00")]
    [InlineData(null, null, 3L, null, "2026-10-30T09:00:00")]
    [InlineData(null, null, null, 1L, "2026-10-26T09:00:00")]
    public void ATimedEvent_FiresItsLeadTimeBeforeTheStart(long? minutes, long? hours, long? days, long? weeks, string expected)
    {
        DateTime fire = ReminderSchedule.FireAtUtc(false, _timedStart, "Asia/Seoul", minutes, hours, days, weeks, new TimeOnly(18, 0));

        Assert.Equal(DateTime.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), fire);
        Assert.Equal(DateTimeKind.Utc, fire.Kind);
    }

    /// <summary>An all-day event's reminder fires N days / weeks before its first day, at the time of day in the calendar's zone.</summary>
    [Theory]
    [InlineData(null, 1L, null, "09:00", "Asia/Seoul", "2026-11-01T00:00:00")]     // 2026-11-01 09:00 KST
    [InlineData(null, 0L, null, "07:30", "Asia/Seoul", "2026-11-01T22:30:00")]     // 2026-11-02 07:30 KST
    [InlineData(null, null, 1L, "09:00", "UTC", "2026-10-26T09:00:00")]
    [InlineData(null, 2L, null, null, "UTC", "2026-10-31T09:00:00")]               // no time of day: 09:00
    [InlineData(30L, null, null, "18:00", "UTC", "2026-11-01T23:30:00")]          // minutes: before midnight of the first day
    public void AnAllDayEvent_FiresOnItsDayAtTheTimeOfDay(long? minutes, long? days, long? weeks, string? time, string zone, string expected)
    {
        DateTime firstDay = new(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc); // a bare date, stored as midnight UTC

        DateTime fire = ReminderSchedule.FireAtUtc(true, firstDay, zone, minutes, null, days, weeks,
            time is null ? null : TimeOnly.Parse(time, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(DateTime.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), fire);
    }

    /// <summary>A time of day inside a spring-forward gap moves to the first instant after it (02:30 does not exist on 2026-03-08 in New York).</summary>
    [Fact]
    public void ATimeInTheSpringForwardGap_MovesToTheEndOfTheGap()
    {
        DateTime fire = ReminderSchedule.FireAtUtc(true, new DateTime(2026, 3, 8, 0, 0, 0, DateTimeKind.Utc), "America/New_York",
            null, null, 0, null, new TimeOnly(2, 30));

        Assert.Equal(new DateTime(2026, 3, 8, 7, 0, 0, DateTimeKind.Utc), fire); // 03:00 EDT
    }

    /// <summary>A time of day that happens twice (fall back) takes the earlier one (01:30 EDT, not 01:30 EST, on 2026-11-01).</summary>
    [Fact]
    public void AnAmbiguousTime_TakesTheEarlierInstant()
    {
        DateTime fire = ReminderSchedule.FireAtUtc(true, new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), "America/New_York",
            null, null, 0, null, new TimeOnly(1, 30));

        Assert.Equal(new DateTime(2026, 11, 1, 5, 30, 0, DateTimeKind.Utc), fire);
    }
}
