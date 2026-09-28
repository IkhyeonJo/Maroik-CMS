using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Calendar;
using Maroik.Core.Service.Mappers;

namespace Maroik.Core.Service.Tests.Mappers;

/// <summary>Unit tests for <see cref="OtherCalendarMapper"/>.</summary>
public class OtherCalendarMapperTests
{
    /// <summary>To response maps all fields.</summary>
    [Fact]
    public void ToResponse_MapsAllFields()
    {
        OtherCalendar other = OtherCalendar.Reconstitute(accountEmail: "henry@example.com", calendarId: 10);

        OtherCalendarResponse response = OtherCalendarMapper.ToResponse(other);

        Assert.Equal("henry@example.com", response.AccountEmail);
        Assert.Equal(10, response.CalendarId);
    }
}
