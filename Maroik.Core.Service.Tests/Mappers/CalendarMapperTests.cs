using Maroik.Core.Contract.Dtos;
using Maroik.Core.Service.Mappers;
using DomainCalendar = Maroik.Core.Domain.Calendar.Calendar;

namespace Maroik.Core.Service.Tests.Mappers;

/// <summary>Unit tests for <see cref="CalendarMapper"/>.</summary>
public class CalendarMapperTests
{
    /// <summary>To response maps all fields.</summary>
    [Fact]
    public void ToResponse_MapsAllFields()
    {
        var created = new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc);
        var updated = new DateTime(2026, 1, 2, 2, 0, 0, DateTimeKind.Utc);

        DomainCalendar calendar = DomainCalendar.Reconstitute(
            id: 10,
            accountEmail: "carol@example.com",
            name: "Work",
            description: "Work related events",
            timeZoneIanaId: "Asia/Seoul",
            htmlColorCode: "#3498DB",
            created: created,
            updated: updated);

        CalendarResponse response = CalendarMapper.ToResponse(calendar);

        Assert.Equal(10, response.Id);
        Assert.Equal("carol@example.com", response.AccountEmail);
        Assert.Equal("Work", response.Name);
        Assert.Equal("Work related events", response.Description);
        Assert.Equal("Asia/Seoul", response.TimeZoneIanaId);
        Assert.Equal("#3498DB", response.HtmlColorCode);
        Assert.Equal(created, response.Created);
        Assert.Equal(updated, response.Updated);
    }
}
