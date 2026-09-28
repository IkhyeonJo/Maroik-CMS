using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Calendar;
using Maroik.Core.Service.Mappers;

namespace Maroik.Core.Service.Tests.Mappers;

/// <summary>Unit tests for <see cref="CalendarEventAttachedFileMapper"/>.</summary>
public class CalendarEventAttachedFileMapperTests
{
    /// <summary>To response maps all fields.</summary>
    [Fact]
    public void ToResponse_MapsAllFields()
    {
        CalendarEventAttachedFile file = CalendarEventAttachedFile.Reconstitute(
            id: 3,
            calendarEventId: 20,
            size: 4096,
            name: "agenda.pdf",
            extension: ".pdf",
            path: "/files/agenda.pdf");

        CalendarEventAttachedFileDto dto = CalendarEventAttachedFileMapper.ToResponse(file);

        Assert.Equal(3, dto.Id);
        Assert.Equal(20, dto.CalendarEventId);
        Assert.Equal(4096, dto.Size);
        Assert.Equal("agenda.pdf", dto.Name);
        Assert.Equal(".pdf", dto.Extension);
        Assert.Equal("/files/agenda.pdf", dto.Path);
    }
}
