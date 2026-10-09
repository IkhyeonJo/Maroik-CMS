using Maroik.Core.Domain.Board;
namespace Maroik.Core.Domain.Tests.Board;

/// <summary>
/// Unit tests for <see cref="BoardAttachedFile"/>.
/// Covers creation validation (name, path, size guards) and reconstitution from trusted data.
/// </summary>
public class BoardAttachedFileTests
{
    // -- Create ---------------------------------------------------------------

    /// <summary>Create returns file, when valid.</summary>
    [Fact]
    public void Create_ReturnsFile_WhenValid()
    {
        var result = BoardAttachedFile.Create(1, 1024, "doc.pdf", ".pdf", "/files/doc.pdf");

        Assert.False(result.IsError);
        Assert.Equal(1, result.Value.BoardId);
        Assert.Equal(1024, result.Value.Size);
        Assert.Equal("doc.pdf", result.Value.Name);
        Assert.Equal(".pdf", result.Value.Extension);
        Assert.Equal("/files/doc.pdf", result.Value.Path);
    }

    /// <summary>Create returns error, when name empty.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ReturnsError_WhenNameEmpty(string? name)
    {
        var result = BoardAttachedFile.Create(1, 1024, name, ".pdf", "/files/doc.pdf");

        Assert.True(result.IsError);
        Assert.Equal("BoardAttachedFile.NameEmpty", result.FirstError.Code);
    }

    /// <summary>Create returns error, when path empty.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ReturnsError_WhenPathEmpty(string? path)
    {
        var result = BoardAttachedFile.Create(1, 1024, "doc.pdf", ".pdf", path);

        Assert.True(result.IsError);
        Assert.Equal("BoardAttachedFile.PathEmpty", result.FirstError.Code);
    }

    /// <summary>Create returns error, when size negative.</summary>
    [Fact]
    public void Create_ReturnsError_WhenSizeNegative()
    {
        var result = BoardAttachedFile.Create(1, -1, "doc.pdf", ".pdf", "/files/doc.pdf");

        Assert.True(result.IsError);
        Assert.Equal("BoardAttachedFile.InvalidSize", result.FirstError.Code);
    }

    /// <summary>Name, extension and path are bounded by their character varying(255) columns: the limit passes, one more is rejected cleanly.</summary>
    [Fact]
    public void Create_EnforcesTheColumnLengthLimits()
    {
        Assert.False(BoardAttachedFile.Create(1, 10, new string("n"[0], 255), ".zip", "/f/a.zip").IsError);
        Assert.False(BoardAttachedFile.Create(1, 10, "doc", new string(".zip"[0], 255), "/f/a.zip").IsError);
        Assert.False(BoardAttachedFile.Create(1, 10, "doc", ".zip", new string("/"[0], 255)).IsError);

        var name = BoardAttachedFile.Create(1, 10, new string("n"[0], 256), ".zip", "/f/a.zip");
        Assert.Equal("BoardAttachedFile.NameTooLong", name.FirstError.Code);
        Assert.Equal("File name must be {0} characters or fewer.", name.FirstError.Metadata!["MessageTemplate"]);

        var extension = BoardAttachedFile.Create(1, 10, "doc", new string(".zip"[0], 256), "/f/a.zip");
        Assert.Equal("BoardAttachedFile.ExtensionTooLong", extension.FirstError.Code);

        var path = BoardAttachedFile.Create(1, 10, "doc", ".zip", new string("/"[0], 256));
        Assert.Equal("BoardAttachedFile.PathTooLong", path.FirstError.Code);
    }

    // -- Reconstitute -----------------------------------------------------------

    /// <summary>Reconstitute rebuilds file without validation.</summary>
    [Fact]
    public void Reconstitute_RebuildsFile_WithoutValidation()
    {
        var file = BoardAttachedFile.Reconstitute(5, 1, 2048, "image.png", ".png", "/files/image.png");

        Assert.Equal(5, file.Id);
        Assert.Equal(1, file.BoardId);
        Assert.Equal(2048, file.Size);
        Assert.Equal("image.png", file.Name);
        Assert.Equal(".png", file.Extension);
        Assert.Equal("/files/image.png", file.Path);
    }

    /// <summary>Reconstitute defaults null name and path to empty string.</summary>
    [Fact]
    public void Reconstitute_DefaultsNullNameAndPath_ToEmptyString()
    {
        var file = BoardAttachedFile.Reconstitute(1, 1, 0, null, null, null);

        Assert.Equal("", file.Name);
        Assert.Equal("", file.Path);
        Assert.Null(file.Extension);
    }
}
