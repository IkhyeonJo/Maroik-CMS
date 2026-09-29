using System.Text;
using Maroik.Website.Extensions;
using Microsoft.AspNetCore.Http;

namespace Maroik.Website.Tests.Extensions;

/// <summary>Unit tests for <see cref="FormFileExtensions"/>.</summary>
public class FormFileExtensionsTests
{
    /// <summary>A form file named <paramref name="fileName"/> with UTF-8 <paramref name="content"/> and <paramref name="contentType"/>.</summary>
    private static FormFile MakeFormFile(string content, string fileName, string contentType)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName) { Headers = new HeaderDictionary(), ContentType = contentType };
    }

    /// <summary>Size limit (10 MiB) passed to the conversion.</summary>
    private const long TenMb = 10L * 1024 * 1024;

    /// <summary>To attached file info async null file returns null.</summary>
    [Fact]
    public async Task ToAttachedFileInfoAsync_NullFile_ReturnsNull()
    {
        IFormFile? file = null;

        var result = await file.ToAttachedFileInfoAsync(TenMb, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    /// <summary>To attached file info async reads bytes content type file name and size.</summary>
    [Fact]
    public async Task ToAttachedFileInfoAsync_ReadsBytesContentTypeFileNameAndSize()
    {
        IFormFile file = MakeFormFile("hello world", "note.txt", "text/plain");

        var result = await file.ToAttachedFileInfoAsync(TenMb, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("hello world", Encoding.UTF8.GetString(result.Bytes));
        Assert.Equal("text/plain", result.ContentType);
        Assert.Equal("note.txt", result.FileName);
        Assert.Equal(11, result.Size);
    }

    /// <summary>
    /// An upload past the size cap returns a body-less descriptor (real Size, no bytes read) so the
    /// service-layer size check rejects it without the payload ever being buffered into memory.
    /// </summary>
    [Fact]
    public async Task ToAttachedFileInfoAsync_OverMaxBytes_ReturnsBodylessDescriptor()
    {
        IFormFile file = MakeFormFile("hello world", "big.zip", "application/zip");

        var result = await file.ToAttachedFileInfoAsync(maxBytes: 4, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Empty(result.Bytes);
        Assert.Equal(11, result.Size);
        Assert.Equal("big.zip", result.FileName);
        Assert.Equal("application/zip", result.ContentType);
    }
}
