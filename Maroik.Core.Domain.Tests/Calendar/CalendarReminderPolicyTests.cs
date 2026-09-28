using Maroik.Core.Domain.Calendar;
namespace Maroik.Core.Domain.Tests.Calendar;

/// <summary>
/// Unit tests for <see cref="CalendarReminderPolicy"/>.
/// Covers the per-unit lead-time bounds (lower bound, upper bound, just past the upper bound,
/// negative) and the no-lead-time-field pass-through.
/// </summary>
public class CalendarReminderPolicyTests
{
    /// <summary>The four per-unit ceilings all describe the same 28-day maximum.</summary>
    [Fact]
    public void MaxConstants_DescribeTheSame28DayCeiling()
    {
        Assert.Equal(CalendarReminderPolicy.MaxDaysBeforeEvent, CalendarReminderPolicy.MaxWeeksBeforeEvent * 7);
        Assert.Equal(CalendarReminderPolicy.MaxHoursBeforeEvent, CalendarReminderPolicy.MaxDaysBeforeEvent * 24);
        Assert.Equal(CalendarReminderPolicy.MaxMinutesBeforeEvent, CalendarReminderPolicy.MaxHoursBeforeEvent * 60);
    }

    /// <summary>Is lead time within range returns true, at the lower bound of each unit.</summary>
    [Theory]
    [InlineData(0L, null, null, null)]
    [InlineData(null, 0L, null, null)]
    [InlineData(null, null, 0L, null)]
    [InlineData(null, null, null, 0L)]
    public void IsLeadTimeWithinRange_ReturnsTrue_AtLowerBound(long? minutes, long? hours, long? days, long? weeks)
    {
        Assert.True(CalendarReminderPolicy.IsLeadTimeWithinRange(minutes, hours, days, weeks));
    }

    /// <summary>Is lead time within range returns true, at the upper bound of each unit.</summary>
    [Theory]
    [InlineData(40320L, null, null, null)]
    [InlineData(null, 672L, null, null)]
    [InlineData(null, null, 28L, null)]
    [InlineData(null, null, null, 4L)]
    public void IsLeadTimeWithinRange_ReturnsTrue_AtUpperBound(long? minutes, long? hours, long? days, long? weeks)
    {
        Assert.True(CalendarReminderPolicy.IsLeadTimeWithinRange(minutes, hours, days, weeks));
    }

    /// <summary>Is lead time within range returns false, one step past the upper bound of each unit.</summary>
    [Theory]
    [InlineData(40321L, null, null, null)]
    [InlineData(null, 673L, null, null)]
    [InlineData(null, null, 29L, null)]
    [InlineData(null, null, null, 5L)]
    public void IsLeadTimeWithinRange_ReturnsFalse_PastUpperBound(long? minutes, long? hours, long? days, long? weeks)
    {
        Assert.False(CalendarReminderPolicy.IsLeadTimeWithinRange(minutes, hours, days, weeks));
    }

    /// <summary>Is lead time within range returns false, when the set value is negative.</summary>
    [Theory]
    [InlineData(-1L, null, null, null)]
    [InlineData(null, -1L, null, null)]
    [InlineData(null, null, -1L, null)]
    [InlineData(null, null, null, -1L)]
    public void IsLeadTimeWithinRange_ReturnsFalse_WhenNegative(long? minutes, long? hours, long? days, long? weeks)
    {
        Assert.False(CalendarReminderPolicy.IsLeadTimeWithinRange(minutes, hours, days, weeks));
    }

    /// <summary>Is lead time within range returns true, when no lead-time field is set.</summary>
    [Fact]
    public void IsLeadTimeWithinRange_ReturnsTrue_WhenNoFieldSet()
    {
        Assert.True(CalendarReminderPolicy.IsLeadTimeWithinRange(null, null, null, null));
    }

    // -- Time-of-day picker settings (serialized into the calendar pages) --------

    /// <summary>The reminder time-of-day step divides an hour evenly and the default lands on it.</summary>
    [Fact]
    public void ReminderTimeOfDaySettings_AreConsistent()
    {
        Assert.True(CalendarReminderPolicy.ReminderTimeOfDayStepMinutes > 0);
        Assert.Equal(0, 60 % CalendarReminderPolicy.ReminderTimeOfDayStepMinutes);

        var parts = CalendarReminderPolicy.DefaultReminderTimeOfDay.Split(':');
        Assert.Equal(2, parts.Length);
        int minute = int.Parse(parts[1]);
        Assert.Equal(0, minute % CalendarReminderPolicy.ReminderTimeOfDayStepMinutes);
    }
}
