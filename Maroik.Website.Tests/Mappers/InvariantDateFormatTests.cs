using System.Globalization;
using Maroik.Core.Contract.Dtos;
using Maroik.Website.Mappers;
using Maroik.Website.Models.ViewModels.AccountBooks;
using Maroik.Website.Models.ViewModels.Calendars;

namespace Maroik.Website.Tests.Mappers;

/// <summary>
/// The date strings the mappers hand to the pages (date pickers, the calendar, the maturity date the edit forms send back)
/// are machine-read <c>yyyy-MM-dd</c> / <c>yyyy-MM-dd HH:mm</c> values, so they must not follow the thread's culture: under a
/// culture with another calendar (th-TH counts Buddhist years — 2568 for 2025) they would carry the wrong year.
/// </summary>
public sealed class InvariantDateFormatTests : IDisposable
{
    /// <summary>The culture the test process ran under, restored afterwards.</summary>
    private readonly CultureInfo _original = CultureInfo.CurrentCulture;

    /// <summary>Runs every test under th-TH.</summary>
    public InvariantDateFormatTests() => CultureInfo.CurrentCulture = new CultureInfo("th-TH");

    /// <summary>Restores the original culture.</summary>
    public void Dispose() => CultureInfo.CurrentCulture = _original;

    /// <summary>A fixed reading of the clock in 2025.</summary>
    private static readonly DateTime UtcNow = new(2025, 6, 15, 3, 0, 0, DateTimeKind.Utc);

    /// <summary>The fixed-schedule maturity dates are Gregorian <c>yyyy-MM-dd</c>.</summary>
    [Fact]
    public void FixedScheduleMaturityDates_AreGregorianIsoDates()
    {
        var income = new FixedIncomeResponse { MaturityDate = new DateTime(2030, 12, 31), DepositMonth = 1, DepositDay = 1 };
        var expenditure = new FixedExpenditureResponse { MaturityDate = new DateTime(2030, 12, 31), DepositMonth = 1, DepositDay = 1 };

        Assert.Equal("2030-12-31", Assert.Single(new[] { income }.ToDisplayViewModels([], k => k, "UTC", 3, UtcNow)).MaturityDate);
        Assert.Equal("2030-12-31", Assert.Single(new[] { expenditure }.ToDisplayViewModels([], k => k, "UTC", 3, UtcNow)).MaturityDate);
    }

    /// <summary>Calendar event display dates (all-day and timed) are Gregorian.</summary>
    [Fact]
    public void CalendarEventDisplayDates_AreGregorian()
    {
        var allDay = new CalendarEventResponse { AllDay = true, StartDate = new DateTime(2025, 6, 15), EndDate = new DateTime(2025, 6, 16) };
        var timed = new CalendarEventResponse { StartDate = new DateTime(2025, 6, 15, 9, 0, 0), EndDate = new DateTime(2025, 6, 15, 10, 30, 0) };

        CalendarEventOutputViewModel allDayVm = allDay.ToDisplayViewModel("UTC", null, forGrid: false);
        CalendarEventOutputViewModel timedVm = timed.ToDisplayViewModel("UTC", null, forGrid: false);

        Assert.Equal(("2025-06-15", "2025-06-16"), (allDayVm.DisplayStartDate, allDayVm.DisplayEndDate));
        Assert.Equal(("2025-06-15 09:00", "2025-06-15 10:30"), (timedVm.DisplayStartDate, timedVm.DisplayEndDate));
    }

    /// <summary>The current date the calendar and account-book pages pre-fill their pickers with is Gregorian.</summary>
    [Fact]
    public void PickerCurrentDates_AreGregorian()
    {
        var calendar = new CalendarOutputViewModel();
        var accountBook = new AccountBookPageViewModel();

        calendar.PopulateTimeData("UTC", UtcNow);
        accountBook.PopulateTimeData("UTC", UtcNow);

        Assert.Equal("2025-06-15", calendar.CurrentDate);
        Assert.Equal("2025-06-15", accountBook.CurrentDate);
    }
}
