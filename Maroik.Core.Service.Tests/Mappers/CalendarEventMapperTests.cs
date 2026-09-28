using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Calendar;
using Maroik.Core.Service.Mappers;

namespace Maroik.Core.Service.Tests.Mappers;

/// <summary>Unit tests for <see cref="CalendarEventMapper"/>.</summary>
public class CalendarEventMapperTests
{
    /// <summary>To response maps all fields.</summary>
    [Fact]
    public void ToResponse_MapsAllFields()
    {
        var start = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
        var created = new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc);
        var updated = new DateTime(2026, 1, 2, 2, 0, 0, DateTimeKind.Utc);

        CalendarEvent evt = CalendarEvent.Reconstitute(
            id: 20,
            calendarId: 10,
            title: "Team Meeting",
            description: "Weekly sync",
            allDay: false,
            startDate: start,
            endDate: end,
            startTz: "Asia/Seoul",
            endTz: "Asia/Seoul",
            location: "Room 101",
            status: "Busy",
            recurrenceId: 99,
            created: created,
            updated: updated);

        CalendarEventResponse response = CalendarEventMapper.ToResponse(evt);

        Assert.Equal(20, response.Id);
        Assert.Equal(10, response.CalendarId);
        Assert.Equal("Team Meeting", response.Title);
        Assert.Equal("Weekly sync", response.Description);
        Assert.False(response.AllDay);
        Assert.Equal(start, response.StartDate);
        Assert.Equal(end, response.EndDate);
        Assert.Equal("Asia/Seoul", response.StartDateTimeZoneIanaId);
        Assert.Equal("Asia/Seoul", response.EndDateTimeZoneIanaId);
        Assert.Equal("Room 101", response.Location);
        Assert.Equal("Busy", response.Status);
        Assert.Equal(99, response.RecurrenceId);
        Assert.Equal(created, response.Created);
        Assert.Equal(updated, response.Updated);
    }
}
