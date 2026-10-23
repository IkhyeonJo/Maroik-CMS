using System.Text;
using Maroik.Core.Domain.FileManagers;
namespace Maroik.Core.Domain.Tests.FileManagers;

/// <summary>
/// Unit tests for <see cref="FileManagerPreviewPolicy"/> (F9): only png / jpg / gif / webp up to 100 MB are previewed, judged by the
/// file's real format (its magic bytes) as well as its extension, and answered with a Content-Type the server sets.
/// </summary>
public class FileManagerPreviewPolicyTests
{
    /// <summary>The leading bytes of each previewable format.</summary>
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0];
    private static readonly byte[] Gif = Encoding.ASCII.GetBytes("GIF89a......");
    private static readonly byte[] Webp = Encoding.ASCII.GetBytes("RIFF\0\0\0\0WEBP");

    /// <summary>The preview size limit is 100 MB.</summary>
    [Fact]
    public void MaxPreviewBytes_Is100Mb() => Assert.Equal(100L * 1024 * 1024, FileManagerPreviewPolicy.MaxPreviewBytes);

    /// <summary>A name with a previewable extension (any case) and a size within the limit can be previewed.</summary>
    [Theory]
    [InlineData("a.png")]
    [InlineData("a.JPG")]
    [InlineData("a.jpeg")]
    [InlineData("a.gif")]
    [InlineData("a.webp")]
    public void IsPreviewable_AcceptsTheImageExtensions(string name)
    {
        Assert.True(FileManagerPreviewPolicy.IsPreviewable(name, 1));
    }

    /// <summary>Every other type has no preview — SVG included, since it can run script — and so has an image over 100 MB.</summary>
    [Theory]
    [InlineData("a.svg", 1L)]
    [InlineData("a.pdf", 1L)]
    [InlineData("a.txt", 1L)]
    [InlineData("a.html", 1L)]
    [InlineData("png", 1L)]
    [InlineData("a.png", 100L * 1024 * 1024 + 1)]
    public void IsPreviewable_RefusesEverythingElse(string name, long size)
    {
        Assert.False(FileManagerPreviewPolicy.IsPreviewable(name, size));
    }

    /// <summary>The Content-Type is the one the magic bytes prove.</summary>
    [Fact]
    public void ContentTypeOf_IsDecidedByTheMagicBytes()
    {
        Assert.Equal("image/png", FileManagerPreviewPolicy.ContentTypeOf(Png));
        Assert.Equal("image/jpeg", FileManagerPreviewPolicy.ContentTypeOf(Jpeg));
        Assert.Equal("image/gif", FileManagerPreviewPolicy.ContentTypeOf(Gif));
        Assert.Equal("image/webp", FileManagerPreviewPolicy.ContentTypeOf(Webp));
    }

    /// <summary>Bytes of another format (an HTML page renamed to .png, an SVG, too few bytes) prove nothing.</summary>
    [Fact]
    public void ContentTypeOf_IsNull_ForAnythingElse()
    {
        Assert.Null(FileManagerPreviewPolicy.ContentTypeOf(Encoding.ASCII.GetBytes("<html><script>")));
        Assert.Null(FileManagerPreviewPolicy.ContentTypeOf(Encoding.ASCII.GetBytes("<svg xmlns=")));
        Assert.Null(FileManagerPreviewPolicy.ContentTypeOf([0x89, 0x50]));
        Assert.Null(FileManagerPreviewPolicy.ContentTypeOf(Encoding.ASCII.GetBytes("RIFF\0\0\0\0WAVE")));
    }
}
