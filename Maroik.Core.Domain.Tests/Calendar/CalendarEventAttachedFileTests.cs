using Maroik.Core.Domain.Calendar;
namespace Maroik.Core.Domain.Tests.Calendar;

/// <summary>
/// Unit tests for <see cref="CalendarEventAttachedFile"/>.
/// Covers creation validation (name, path, size guards) and reconstitution from trusted data.
/// </summary>
public class CalendarEventAttachedFileTests
{
    // -- Create ---------------------------------------------------------------

    /// <summary>Create returns file, when valid.</summary>
    [Fact]
    public void Create_ReturnsFile_WhenValid()
    {
        var result = CalendarEventAttachedFile.Create(1, 512, "agenda.pdf", ".pdf", "/files/agenda.pdf");

        Assert.False(result.IsError);
        Assert.Equal(1, result.Value.CalendarEventId);
        Assert.Equal(512, result.Value.Size);
        Assert.Equal("agenda.pdf", result.Value.Name);
        Assert.Equal(".pdf", result.Value.Extension);
        Assert.Equal("/files/agenda.pdf", result.Value.Path);
    }

    /// <summary>Create returns error, when name empty.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ReturnsError_WhenNameEmpty(string? name)
    {
        var result = CalendarEventAttachedFile.Create(1, 512, name, ".pdf", "/files/agenda.pdf");

        Assert.True(result.IsError);
        Assert.Equal("CalendarEventAttachedFile.NameEmpty", result.FirstError.Code);
    }

    /// <summary>Create returns error, when path empty.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ReturnsError_WhenPathEmpty(string? path)
    {
        var result = CalendarEventAttachedFile.Create(1, 512, "agenda.pdf", ".pdf", path);

        Assert.True(result.IsError);
        Assert.Equal("CalendarEventAttachedFile.PathEmpty", result.FirstError.Code);
    }

    /// <summary>Create returns error, when size negative.</summary>
    [Fact]
    public void Create_ReturnsError_WhenSizeNegative()
    {
        var result = CalendarEventAttachedFile.Create(1, -1, "agenda.pdf", ".pdf", "/files/agenda.pdf");

        Assert.True(result.IsError);
        Assert.Equal("CalendarEventAttachedFile.InvalidSize", result.FirstError.Code);
    }

    /// <summary>Name, extension and path are bounded by their character varying(255) columns: the limit passes, one more is rejected cleanly.</summary>
    [Fact]
    public void Create_EnforcesTheColumnLengthLimits()
    {
        Assert.False(CalendarEventAttachedFile.Create(1, 10, new string("n"[0], 255), ".zip", "/f/a.zip").IsError);
        Assert.False(CalendarEventAttachedFile.Create(1, 10, "doc", new string(".zip"[0], 255), "/f/a.zip").IsError);
        Assert.False(CalendarEventAttachedFile.Create(1, 10, "doc", ".zip", new string("/"[0], 255)).IsError);

        var name = CalendarEventAttachedFile.Create(1, 10, new string("n"[0], 256), ".zip", "/f/a.zip");
        Assert.Equal("CalendarEventAttachedFile.NameTooLong", name.FirstError.Code);
        Assert.Equal("File name must be {0} characters or fewer.", name.FirstError.Metadata!["MessageTemplate"]);

        var extension = CalendarEventAttachedFile.Create(1, 10, "doc", new string(".zip"[0], 256), "/f/a.zip");
        Assert.Equal("CalendarEventAttachedFile.ExtensionTooLong", extension.FirstError.Code);

        var path = CalendarEventAttachedFile.Create(1, 10, "doc", ".zip", new string("/"[0], 256));
        Assert.Equal("CalendarEventAttachedFile.PathTooLong", path.FirstError.Code);
    }

    // -- Reconstitute -----------------------------------------------------------

    /// <summary>Reconstitute rebuilds file without validation.</summary>
    [Fact]
    public void Reconstitute_RebuildsFile_WithoutValidation()
    {
        var file = CalendarEventAttachedFile.Reconstitute(5, 1, 2048, "slides.pptx", ".pptx", "/files/slides.pptx");

        Assert.Equal(5, file.Id);
        Assert.Equal(1, file.CalendarEventId);
        Assert.Equal(2048, file.Size);
        Assert.Equal("slides.pptx", file.Name);
        Assert.Equal(".pptx", file.Extension);
        Assert.Equal("/files/slides.pptx", file.Path);
    }

    /// <summary>Reconstitute defaults null name and path to empty string.</summary>
    [Fact]
    public void Reconstitute_DefaultsNullNameAndPath_ToEmptyString()
    {
        var file = CalendarEventAttachedFile.Reconstitute(1, 1, 0, null, null, null);

        Assert.Equal("", file.Name);
        Assert.Equal("", file.Path);
        Assert.Null(file.Extension);
    }
}
