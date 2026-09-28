using Maroik.Core.Domain.Media;
namespace Maroik.Core.Domain.Tests.Media;

/// <summary>
/// Unit tests for <see cref="AttachmentUploadPolicy"/> — accepted extensions
/// and the caller-supplied size cap check.
/// </summary>
public class AttachmentUploadPolicyTests
{
    /// <summary>Allowed extensions are the zip set.</summary>
    [Fact]
    public void AllowedExtensions_IsZipOnly()
    {
        Assert.Equal([".zip"], AttachmentUploadPolicy.AllowedExtensions);
    }

    /// <summary>Is allowed extension is case-insensitive and rejects everything else.</summary>
    [Theory]
    [InlineData(".zip", true)]
    [InlineData(".ZIP", true)]
    [InlineData(".Zip", true)]
    [InlineData(".rar", false)]
    [InlineData(".7z", false)]
    [InlineData("zip", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsAllowedExtension_Works(string? extension, bool expected)
    {
        Assert.Equal(expected, AttachmentUploadPolicy.IsAllowedExtension(extension));
    }

    /// <summary>Is valid size rejects non-positive and over-cap sizes, accepts everything in between.</summary>
    [Theory]
    [InlineData(0, 100, false)]
    [InlineData(-1, 100, false)]
    [InlineData(1, 100, true)]
    [InlineData(100, 100, true)]
    [InlineData(101, 100, false)]
    public void IsValidSize_Works(long sizeBytes, long maxBytes, bool expected)
    {
        Assert.Equal(expected, AttachmentUploadPolicy.IsValidSize(sizeBytes, maxBytes));
    }
}
