using Maroik.Core.Domain.Finance;
namespace Maroik.Core.Domain.Tests.Finance;

/// <summary>
/// Unit tests for <see cref="FixedSchedulePolicy"/>.
/// Covers notice-window detection, unpunctuality override, invalid date combinations,
/// expiration detection (including the exact-boundary case), and row CSS class selection.
/// </summary>
public class FixedSchedulePolicyTests
{
    // -- IsNoticed --------------------------------------------------------------

    /// <summary>Is noticed returns true, when deposit date within window.</summary>
    [Fact]
    public void IsNoticed_ReturnsTrue_WhenDepositDateWithinWindow()
    {
        var today = new DateTime(2026, 7, 20);

        var result = FixedSchedulePolicy.IsNoticed(depositMonth: 7, depositDay: 23, today, noticeWindowDays: 5, unpunctuality: false);

        Assert.True(result);
    }

    /// <summary>Is noticed returns true, when deposit date is exactly at window boundary.</summary>
    [Fact]
    public void IsNoticed_ReturnsTrue_WhenDepositDateIsExactlyAtWindowBoundary()
    {
        var today = new DateTime(2026, 7, 20);

        var result = FixedSchedulePolicy.IsNoticed(depositMonth: 7, depositDay: 25, today, noticeWindowDays: 5, unpunctuality: false);

        Assert.True(result);
    }

    /// <summary>Is noticed returns false, when deposit date just outside window.</summary>
    [Fact]
    public void IsNoticed_ReturnsFalse_WhenDepositDateJustOutsideWindow()
    {
        var today = new DateTime(2026, 7, 20);

        var result = FixedSchedulePolicy.IsNoticed(depositMonth: 7, depositDay: 26, today, noticeWindowDays: 5, unpunctuality: false);

        Assert.False(result);
    }

    /// <summary>Is noticed returns false, when deposit date already passed.</summary>
    [Fact]
    public void IsNoticed_ReturnsFalse_WhenDepositDateAlreadyPassed()
    {
        var today = new DateTime(2026, 7, 20);

        var result = FixedSchedulePolicy.IsNoticed(depositMonth: 7, depositDay: 19, today, noticeWindowDays: 5, unpunctuality: false);

        Assert.False(result);
    }

    /// <summary>
    /// Regression test (year-end): the next deposit is the nearest one on or after today, so a
    /// January deposit is noticed from late December — it used to be compared with this year's
    /// already-passed January date and never noticed.
    /// </summary>
    [Theory]
    [InlineData(12, 28, 1, 2, 7, true)]   // 5 days ahead, across the new year
    [InlineData(12, 31, 1, 1, 1, true)]   // tomorrow, in the next year
    [InlineData(12, 31, 1, 1, 0, false)]  // tomorrow, but a zero-day window only notices the day itself
    [InlineData(12, 20, 1, 5, 30, true)]  // 16 days ahead
    [InlineData(12, 20, 1, 5, 15, false)] // 16 days ahead, one beyond a 15-day window
    [InlineData(12, 20, 1, 5, 16, true)]  // exactly at the window's edge
    public void IsNoticed_LooksAtNextYearsDeposit_OnceThisYearsHasPassed(
        int todayMonth, int todayDay, int depositMonth, int depositDay, int window, bool expected)
    {
        var today = new DateTime(2026, todayMonth, todayDay);

        Assert.Equal(expected, FixedSchedulePolicy.IsNoticed(depositMonth, depositDay, today, window, unpunctuality: false));
    }

    /// <summary>The window edges on the deposit's own year: 0 days (the day itself), N days, and N+1 days.</summary>
    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(1, 0, false)]
    [InlineData(7, 7, true)]
    [InlineData(8, 7, false)]
    public void IsNoticed_WindowBoundaries(int daysAhead, int window, bool expected)
    {
        var today = new DateTime(2026, 7, 20);
        DateTime deposit = today.AddDays(daysAhead);

        Assert.Equal(expected, FixedSchedulePolicy.IsNoticed(deposit.Month, deposit.Day, today, window, unpunctuality: false));
    }

    /// <summary>A (month, day) pair that exists in no year is never noticed, and never throws.</summary>
    [Theory]
    [InlineData(13, 1)]
    [InlineData(0, 1)]
    [InlineData(6, 0)]
    [InlineData(6, 31)]
    public void IsNoticed_ReturnsFalse_ForADateThatNeverExists(int depositMonth, int depositDay)
    {
        var today = new DateTime(2026, 6, 1);

        Assert.False(FixedSchedulePolicy.IsNoticed(depositMonth, depositDay, today, noticeWindowDays: 400, unpunctuality: false));
    }

    /// <summary>A February 29 deposit is noticed in a leap year, inside the window.</summary>
    [Fact]
    public void IsNoticed_ReturnsTrue_ForFebruary29_InALeapYear()
    {
        var today = new DateTime(2028, 2, 25);

        Assert.True(FixedSchedulePolicy.IsNoticed(depositMonth: 2, depositDay: 29, today, noticeWindowDays: 7, unpunctuality: false));
    }

    /// <summary>
    /// Once a leap year's February 29 has passed, the next one is four years away — not next year,
    /// where the date does not exist.
    /// </summary>
    [Fact]
    public void IsNoticed_ReturnsFalse_ForFebruary29_AfterItHasPassedInALeapYear()
    {
        var today = new DateTime(2028, 3, 1);

        Assert.False(FixedSchedulePolicy.IsNoticed(depositMonth: 2, depositDay: 29, today, noticeWindowDays: 365, unpunctuality: false));
    }

    /// <summary>Same year-end scenario for a different already-passed month/day combination.</summary>
    [Fact]
    public void IsNoticed_ReturnsFalse_WhenThisYearsOccurrenceAlreadyPassed_March()
    {
        var today = new DateTime(2026, 12, 20);

        var result = FixedSchedulePolicy.IsNoticed(depositMonth: 3, depositDay: 1, today, noticeWindowDays: 30, unpunctuality: false);

        Assert.False(result);
    }

    /// <summary>Is noticed returns true when unpunctuality true regardless of date.</summary>
    [Fact]
    public void IsNoticed_ReturnsTrue_WhenUnpunctualityTrue_RegardlessOfDate()
    {
        var today = new DateTime(2026, 7, 20);

        var result = FixedSchedulePolicy.IsNoticed(depositMonth: 1, depositDay: 1, today, noticeWindowDays: 0, unpunctuality: true);

        Assert.True(result);
    }

    /// <summary>Is noticed returns false for invalid date combination.</summary>
    [Fact]
    public void IsNoticed_ReturnsFalse_ForInvalidDateCombination()
    {
        // Feb 29 in a non-leap year (2026) is invalid.
        var today = new DateTime(2026, 2, 20);

        var result = FixedSchedulePolicy.IsNoticed(depositMonth: 2, depositDay: 29, today, noticeWindowDays: 10, unpunctuality: false);

        Assert.False(result);
    }

    // -- IsValidDepositDate -------------------------------------------------------

    /// <summary>Is valid deposit date returns true, for the last day of a 31-day month.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(12)]
    public void IsValidDepositDate_ReturnsTrue_ForDay31Of31DayMonth(int month)
    {
        Assert.True(FixedSchedulePolicy.IsValidDepositDate(month, 31));
    }

    /// <summary>Is valid deposit date returns false, for day 31 of a 30-day month.</summary>
    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(9)]
    [InlineData(11)]
    public void IsValidDepositDate_ReturnsFalse_ForDay31Of30DayMonth(int month)
    {
        Assert.False(FixedSchedulePolicy.IsValidDepositDate(month, 31));
    }

    /// <summary>Is valid deposit date returns true, for day 30 of a 30-day month.</summary>
    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(9)]
    [InlineData(11)]
    public void IsValidDepositDate_ReturnsTrue_ForDay30Of30DayMonth(int month)
    {
        Assert.True(FixedSchedulePolicy.IsValidDepositDate(month, 30));
    }

    /// <summary>Is valid deposit date returns true, for day 29 in February (independent of leap year).</summary>
    [Fact]
    public void IsValidDepositDate_ReturnsTrue_ForFebruary29()
    {
        Assert.True(FixedSchedulePolicy.IsValidDepositDate(2, 29));
    }

    /// <summary>Is valid deposit date returns false, for day 30 in February.</summary>
    [Fact]
    public void IsValidDepositDate_ReturnsFalse_ForFebruary30()
    {
        Assert.False(FixedSchedulePolicy.IsValidDepositDate(2, 30));
    }

    /// <summary>Is valid deposit date returns false, for an out-of-range month.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void IsValidDepositDate_ReturnsFalse_ForMonthOutOfRange(int month)
    {
        Assert.False(FixedSchedulePolicy.IsValidDepositDate(month, 1));
    }

    // -- MaxDepositDay ----------------------------------------------------------

    /// <summary>Max deposit day matches the 31/30/29 month groups and 0 outside 1–12.</summary>
    [Theory]
    [InlineData(1, 31)]
    [InlineData(2, 29)]
    [InlineData(3, 31)]
    [InlineData(4, 30)]
    [InlineData(5, 31)]
    [InlineData(6, 30)]
    [InlineData(7, 31)]
    [InlineData(8, 31)]
    [InlineData(9, 30)]
    [InlineData(10, 31)]
    [InlineData(11, 30)]
    [InlineData(12, 31)]
    [InlineData(0, 0)]
    [InlineData(13, 0)]
    public void MaxDepositDay_MatchesMonthGroups(int month, int expected)
    {
        Assert.Equal(expected, FixedSchedulePolicy.MaxDepositDay(month));
    }

    /// <summary>The serialized month→day map has one entry per calendar month, each equal to <see cref="FixedSchedulePolicy.MaxDepositDay"/>.</summary>
    [Fact]
    public void MaxDepositDayByMonth_CoversEveryMonth_AndAgreesWithMaxDepositDay()
    {
        Assert.Equal(12, FixedSchedulePolicy.MaxDepositDayByMonth.Count);
        for (int month = 1; month <= 12; month++)
            Assert.Equal(FixedSchedulePolicy.MaxDepositDay(month), FixedSchedulePolicy.MaxDepositDayByMonth[month]);
    }

    // -- IsExpired ----------------------------------------------------------------

    /// <summary>Is expired returns true, when maturity date before today.</summary>
    [Fact]
    public void IsExpired_ReturnsTrue_WhenMaturityDateBeforeToday()
    {
        var maturityDate = new DateTime(2026, 7, 1);
        var today = new DateTime(2026, 7, 2);

        Assert.True(FixedSchedulePolicy.IsExpired(maturityDate, today));
    }

    /// <summary>Is expired returns false, when maturity date equals today.</summary>
    [Fact]
    public void IsExpired_ReturnsFalse_WhenMaturityDateEqualsToday()
    {
        var date = new DateTime(2026, 7, 2);

        Assert.False(FixedSchedulePolicy.IsExpired(date, date));
    }

    /// <summary>Is expired returns false, when maturity date after today.</summary>
    [Fact]
    public void IsExpired_ReturnsFalse_WhenMaturityDateAfterToday()
    {
        var maturityDate = new DateTime(2026, 7, 3);
        var today = new DateTime(2026, 7, 2);

        Assert.False(FixedSchedulePolicy.IsExpired(maturityDate, today));
    }

    // -- IsAcceptableMaturityDate -------------------------------------------------

    /// <summary>Is acceptable maturity date returns true, when maturity date is in the future.</summary>
    [Fact]
    public void IsAcceptableMaturityDate_ReturnsTrue_WhenInFuture()
    {
        var todayUtc = new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc);

        Assert.True(FixedSchedulePolicy.IsAcceptableMaturityDate(new DateTime(2027, 1, 1), todayUtc));
    }

    /// <summary>Is acceptable maturity date returns true, when maturity date equals today.</summary>
    [Fact]
    public void IsAcceptableMaturityDate_ReturnsTrue_WhenEqualsToday()
    {
        var todayUtc = new DateTime(2026, 7, 2, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(FixedSchedulePolicy.IsAcceptableMaturityDate(new DateTime(2026, 7, 2), todayUtc));
    }

    /// <summary>
    /// Is acceptable maturity date returns true one day before today: the timezone grace so a
    /// client behind UTC entering its own "today" is not rejected.
    /// </summary>
    [Fact]
    public void IsAcceptableMaturityDate_ReturnsTrue_WithinOneDayGrace()
    {
        var todayUtc = new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc);

        Assert.True(FixedSchedulePolicy.IsAcceptableMaturityDate(new DateTime(2026, 7, 1), todayUtc));
    }

    /// <summary>Is acceptable maturity date returns false, when maturity date is clearly in the past.</summary>
    [Fact]
    public void IsAcceptableMaturityDate_ReturnsFalse_WhenClearlyPast()
    {
        var todayUtc = new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc);

        Assert.False(FixedSchedulePolicy.IsAcceptableMaturityDate(new DateTime(2026, 6, 30), todayUtc));
    }

    /// <summary>The "no maturity date" sentinel is always acceptable.</summary>
    [Fact]
    public void IsAcceptableMaturityDate_ReturnsTrue_ForNoMaturityDateSentinel()
    {
        Assert.True(FixedSchedulePolicy.IsAcceptableMaturityDate(FixedSchedulePolicy.NoMaturityDate, DateTime.UtcNow));
    }

    /// <summary>The published sentinel string is the ISO form of <see cref="FixedSchedulePolicy.NoMaturityDate"/>.</summary>
    [Fact]
    public void NoMaturityDateIso_IsTheIsoFormOfNoMaturityDate()
    {
        Assert.Equal("9999-12-31", FixedSchedulePolicy.NoMaturityDateIso);
        Assert.Equal(FixedSchedulePolicy.NoMaturityDate.Date, DateTime.Parse(FixedSchedulePolicy.NoMaturityDateIso).Date);
    }

    // -- GetRowStatus -------------------------------------------------------------

    /// <summary>Get row status returns Expired when expired even if also noticed.</summary>
    [Fact]
    public void GetRowStatus_ReturnsExpired_WhenExpired_EvenIfAlsoNoticed()
    {
        Assert.Equal(FixedScheduleRowStatus.Expired, FixedSchedulePolicy.GetRowStatus(expired: true, noticed: true));
    }

    /// <summary>Get row status returns Noticed, when noticed and not expired.</summary>
    [Fact]
    public void GetRowStatus_ReturnsNoticed_WhenNoticedAndNotExpired()
    {
        Assert.Equal(FixedScheduleRowStatus.Noticed, FixedSchedulePolicy.GetRowStatus(expired: false, noticed: true));
    }

    /// <summary>Get row status returns Normal, when neither expired nor noticed.</summary>
    [Fact]
    public void GetRowStatus_ReturnsNormal_WhenNeitherExpiredNorNoticed()
    {
        Assert.Equal(FixedScheduleRowStatus.Normal, FixedSchedulePolicy.GetRowStatus(expired: false, noticed: false));
    }
}
