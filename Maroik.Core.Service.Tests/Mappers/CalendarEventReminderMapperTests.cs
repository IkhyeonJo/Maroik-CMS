using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Calendar;
using Maroik.Core.Service.Mappers;

namespace Maroik.Core.Service.Tests.Mappers;

/// <summary>Unit tests for <see cref="CalendarEventReminderMapper"/>.</summary>
public class CalendarEventReminderMapperTests
{
    /// <summary>To response maps all fields.</summary>
    [Fact]
    public void ToResponse_MapsAllFields()
    {
        var timeOfDay = new TimeOnly(9, 30);

        CalendarEventReminder reminder = CalendarEventReminder.Reconstitute(
            id: 8,
            calendarEventId: 20,
            method: "Email",
            minutesBefore: 15,
            hoursBefore: 1,
            daysBefore: 2,
            weeksBefore: 1,
            timesBefore: timeOfDay);

        CalendarEventReminderDto dto = CalendarEventReminderMapper.ToResponse(reminder);

        Assert.Equal(8, dto.Id);
        Assert.Equal(20, dto.CalendarEventId);
        Assert.Equal("Email", dto.Method);
        Assert.Equal(15, dto.MinutesBeforeEvent);
        Assert.Equal(1, dto.HoursBeforeEvent);
        Assert.Equal(2, dto.DaysBeforeEvent);
        Assert.Equal(1, dto.WeeksBeforeEvent);
        Assert.Equal(timeOfDay, dto.TimesBeforeEvent);
    }
}
