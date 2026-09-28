using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Board;
using Maroik.Core.Service.Mappers;

namespace Maroik.Core.Service.Tests.Mappers;

/// <summary>Unit tests for <see cref="BoardAttachedFileMapper"/>.</summary>
public class BoardAttachedFileMapperTests
{
    /// <summary>To response maps all fields.</summary>
    [Fact]
    public void ToResponse_MapsAllFields()
    {
        BoardAttachedFile file = BoardAttachedFile.Reconstitute(
            id: 7,
            boardId: 42,
            size: 2048,
            name: "report.pdf",
            extension: ".pdf",
            path: "/files/report.pdf");

        BoardAttachedFileDto dto = BoardAttachedFileMapper.ToResponse(file);

        Assert.Equal(7, dto.Id);
        Assert.Equal(42, dto.BoardId);
        Assert.Equal(2048, dto.Size);
        Assert.Equal("report.pdf", dto.Name);
        Assert.Equal(".pdf", dto.Extension);
        Assert.Equal("/files/report.pdf", dto.Path);
    }
}
