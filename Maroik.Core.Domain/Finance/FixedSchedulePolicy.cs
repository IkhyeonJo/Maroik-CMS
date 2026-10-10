using System.Globalization;

namespace Maroik.Core.Domain.Finance;

/// <summary>
/// Domain rules for determining the notification status of a recurring fixed-income or
/// fixed-expenditure schedule.
/// These rules are shared between the presentation layer (notice grid display)
/// and the application layer (dashboard badge counts).
/// </summary>
public static class FixedSchedulePolicy
{
    /// <summary>
    /// Sentinel <c>MaturityDate</c> meaning "this schedule never expires". Far enough in the
    /// future that <see cref="IsExpired"/> can never treat a live schedule as past it, and that
    /// <see cref="IsAcceptableMaturityDate"/> always accepts it — "never expires" needs no
    /// special case. The create / edit forms serialize <see cref="NoMaturityDateIso"/> so the
    /// client no longer hard-codes the same date.
    /// </summary>
    public static readonly DateTime NoMaturityDate = new(9999, 12, 31);

    /// <summary><see cref="NoMaturityDate"/> as the <c>yyyy-MM-dd</c> string the forms submit and render.</summary>
    public static readonly string NoMaturityDateIso =
        NoMaturityDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="maturityDate"/> is acceptable for a
    /// newly registered or newly changed schedule: not already in the past, mirroring the
    /// create / edit form's <c>minDate: 0</c>. A one-day grace against <paramref name="todayUtc"/>
    /// keeps a client in a timezone behind UTC from being rejected for entering its own "today"
    /// during the last hours of the day; anything genuinely in the past still fails.
    /// <see cref="NoMaturityDate"/> passes naturally.
    /// </summary>
    public static bool IsAcceptableMaturityDate(DateTime maturityDate, DateTime todayUtc)
        => maturityDate.Date >= todayUtc.Date.AddDays(-1);

    /// <summary>
    /// Returns <see langword="true"/> when the next scheduled deposit for
    /// (<paramref name="depositMonth"/>, <paramref name="depositDay"/>) — the nearest date on or
    /// after <paramref name="today"/> that exists, this year or next year — falls within
    /// <paramref name="noticeWindowDays"/> days of <paramref name="today"/>, or when
    /// <paramref name="unpunctuality"/> is set. A pair that does not exist in a year (February 29
    /// outside a leap year) has no deposit that year, so it is not noticed then.
    /// </summary>
    public static bool IsNoticed(
        int depositMonth,
        int depositDay,
        DateTime today,
        int noticeWindowDays,
        bool unpunctuality)
    {
        if (unpunctuality) return true;

        // Compare on whole calendar days, independent of the caller's time-of-day: a non-midnight
        // "today" would otherwise flip the "noticed on the deposit day itself" result.
        today = today.Date;
        DateTime? nextDeposit = DepositDateIn(today.Year, depositMonth, depositDay) is { } thisYear && thisYear >= today
            ? thisYear
            : DepositDateIn(today.Year + 1, depositMonth, depositDay);

        return nextDeposit is { } deposit && (deposit - today).TotalDays <= noticeWindowDays;
    }

    /// <summary>
    /// The deposit date for (<paramref name="month"/>, <paramref name="day"/>) in
    /// <paramref name="year"/>, or <see langword="null"/> when that date does not exist in that
    /// year (e.g. February 29 outside a leap year, or an out-of-range month or day).
    /// </summary>
    private static DateTime? DepositDateIn(int year, int month, int day)
        => month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month)
            ? new DateTime(year, month, day)
            : null;

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="maturityDate"/> is strictly
    /// before <paramref name="today"/> (i.e. the schedule has expired).
    /// </summary>
    public static bool IsExpired(DateTime maturityDate, DateTime today)
        => maturityDate.Date < today.Date;

    /// <summary>
    /// The highest valid deposit day for <paramref name="depositMonth"/> (1–12): 31 for 31-day
    /// months, 30 for 30-day months, 29 for February (regardless of leap year — kept from the original
    /// per-month day-range validation), and 0 for an out-of-range month.
    /// <para>
    /// Single source of truth for the month→day rule: <see cref="IsValidDepositDate"/> is derived
    /// from it, and the presentation layer serializes <see cref="MaxDepositDayByMonth"/> into the
    /// create/edit forms so the client no longer hard-codes the same month groups.
    /// </para>
    /// </summary>
    public static int MaxDepositDay(int depositMonth) => depositMonth switch
    {
        1 or 3 or 5 or 7 or 8 or 10 or 12 => 31,
        4 or 6 or 9 or 11 => 30,
        2 => 29,
        _ => 0
    };

    /// <summary>
    /// <see cref="MaxDepositDay(int)"/> for every month 1–12, for serialization into the UI.
    /// </summary>
    public static readonly IReadOnlyDictionary<int, int> MaxDepositDayByMonth =
        Enumerable.Range(1, 12).ToDictionary(month => month, MaxDepositDay);

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="depositDay"/> is a valid day for
    /// <paramref name="depositMonth"/> (30/31-day months, and February capped at 29 regardless
    /// of leap year — kept from the original per-month day-range validation).
    /// </summary>
    public static bool IsValidDepositDate(int depositMonth, int depositDay)
        => depositDay >= 1 && depositDay <= MaxDepositDay(depositMonth);

    /// <summary>
    /// Returns the notice-grid row status for a schedule, prioritizing
    /// <paramref name="expired"/> over <paramref name="noticed"/> over the default state.
    /// The presentation layer maps this to a CSS class; Domain only decides the status.
    /// </summary>
    public static FixedScheduleRowStatus GetRowStatus(bool expired, bool noticed) =>
        expired ? FixedScheduleRowStatus.Expired : noticed ? FixedScheduleRowStatus.Noticed : FixedScheduleRowStatus.Normal;
}

/// <summary>Notice-grid row status for a fixed schedule, as decided by <see cref="FixedSchedulePolicy.GetRowStatus"/>.</summary>
public enum FixedScheduleRowStatus
{
    /// <summary>No further recurrences are expected; the maturity date has passed.</summary>
    Expired,

    /// <summary>The next recurrence is approaching within the notification window.</summary>
    Noticed,

    /// <summary>Neither expired nor noticed.</summary>
    Normal
}
