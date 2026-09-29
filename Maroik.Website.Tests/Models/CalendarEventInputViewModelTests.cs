using System.ComponentModel.DataAnnotations;
using Maroik.Website.Models.ViewModels.Calendar;

namespace Maroik.Website.Tests.Models;

/// <summary>
/// Tests for <see cref="CalendarEventInputViewModel.Validate"/>: the date / time-zone fields the mapper
/// parses must be present and well-formed, so a bad form fails as a validation error instead of throwing
/// from inside the mapper.
/// </summary>
public class CalendarEventInputViewModelTests
{
    /// <summary>The member names of every validation error of <paramref name="vm"/>.</summary>
    private static List<string> Errors(CalendarEventInputViewModel vm) =>
        [.. vm.Validate(new ValidationContext(vm)).SelectMany(r => r.MemberNames)];

    /// <summary>A well-formed timed event (start without zero padding) spanning two time zones.</summary>
    private static CalendarEventInputViewModel Timed() => new()
    {
        AllDay = false, StartDate = "2024-5-1 9:5", EndDate = "2024-05-01 10:30",
        StartDateTimeZoneIanaId = "UTC", EndDateTimeZoneIanaId = "Asia/Seoul",
    };

    /// <summary>A well-formed timed event (with or without zero padding) has no errors.</summary>
    [Fact]
    public void Timed_WellFormed_HasNoErrors() => Assert.Empty(Errors(Timed()));

    /// <summary>A well-formed all-day event needs only the two dates (no time zones).</summary>
    [Fact]
    public void AllDay_WellFormed_HasNoErrors_AndNeedsNoTimeZones() =>
        Assert.Empty(Errors(new CalendarEventInputViewModel { AllDay = true, StartDate = "2024-06-01", EndDate = "2024-6-3" }));

    /// <summary>All-day: a missing, time-carrying or malformed date is reported against that date field.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2024-06-01 10:00")]
    [InlineData("06/01/2024")]
    [InlineData("2024-13-01")]
    public void AllDay_BadDates_AreReportedPerField(string? bad)
    {
        Assert.Equal([nameof(CalendarEventInputViewModel.StartDate)],
            Errors(new CalendarEventInputViewModel { AllDay = true, StartDate = bad, EndDate = "2024-06-03" }));
        Assert.Equal([nameof(CalendarEventInputViewModel.EndDate)],
            Errors(new CalendarEventInputViewModel { AllDay = true, StartDate = "2024-06-01", EndDate = bad }));
    }

    /// <summary>Timed: a date without a time, or garbage, is reported against that date field.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("2024-06-01")]
    [InlineData("2024-06-01T10:00")]
    [InlineData("not a date")]
    public void Timed_BadDates_AreReportedPerField(string? bad)
    {
        CalendarEventInputViewModel start = Timed(); start.StartDate = bad;
        CalendarEventInputViewModel end = Timed(); end.EndDate = bad;
        Assert.Equal([nameof(CalendarEventInputViewModel.StartDate)], Errors(start));
        Assert.Equal([nameof(CalendarEventInputViewModel.EndDate)], Errors(end));
    }

    /// <summary>Timed: each time zone is required independently.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Timed_MissingTimeZones_AreReportedPerField(string? blank)
    {
        CalendarEventInputViewModel start = Timed(); start.StartDateTimeZoneIanaId = blank;
        CalendarEventInputViewModel end = Timed(); end.EndDateTimeZoneIanaId = blank;
        Assert.Equal([nameof(CalendarEventInputViewModel.StartDateTimeZoneIanaId)], Errors(start));
        Assert.Equal([nameof(CalendarEventInputViewModel.EndDateTimeZoneIanaId)], Errors(end));
    }

    /// <summary>An empty timed form reports all four problems at once.</summary>
    [Fact]
    public void Timed_Empty_ReportsEveryField() =>
        Assert.Equal(4, Errors(new CalendarEventInputViewModel { AllDay = false }).Count);
}
