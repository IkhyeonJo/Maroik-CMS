using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Calendar;
using Maroik.Core.Service.Mappers;

namespace Maroik.Core.Service.Tests.Mappers;

/// <summary>Unit tests for <see cref="CalendarSharedMapper"/>.</summary>
public class CalendarSharedMapperTests
{
    /// <summary>To response maps all fields.</summary>
    [Fact]
    public void ToResponse_MapsAllFields()
    {
        CalendarShared shared = CalendarShared.Reconstitute(calendarId: 10, user: true, anonymous: false);

        CalendarSharedResponse response = CalendarSharedMapper.ToResponse(shared);

        Assert.Equal(10, response.CalendarId);
        Assert.True(response.User);
        Assert.False(response.Anonymous);
    }
}
