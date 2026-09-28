using Maroik.Core.Domain.Media;
namespace Maroik.Core.Domain.Tests.Media;

/// <summary>
/// Unit tests for <see cref="ImageUploadPolicy"/> — accepted extensions/content types
/// and their case-insensitive membership checks.
/// </summary>
public class ImageUploadPolicyTests
{
    /// <summary>Allowed extensions/content types are the jpeg/png set.</summary>
    [Fact]
    public void AllowedSets_AreJpegAndPng()
    {
        Assert.Equal([".jpg", ".jpeg", ".png"], ImageUploadPolicy.AllowedExtensions);
        Assert.Equal(["image/jpeg", "image/png"], ImageUploadPolicy.AllowedContentTypes);
    }

    /// <summary>Is allowed extension is case-insensitive and rejects everything else.</summary>
    [Theory]
    [InlineData(".jpg", true)]
    [InlineData(".JPEG", true)]
    [InlineData(".Png", true)]
    [InlineData(".gif", false)]
    [InlineData(".svg", false)]
    [InlineData("jpg", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsAllowedExtension_Works(string? extension, bool expected)
    {
        Assert.Equal(expected, ImageUploadPolicy.IsAllowedExtension(extension));
    }

    /// <summary>Is allowed content type is case-insensitive and rejects everything else.</summary>
    [Theory]
    [InlineData("image/jpeg", true)]
    [InlineData("IMAGE/PNG", true)]
    [InlineData("image/gif", false)]
    [InlineData("image/svg+xml", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsAllowedContentType_Works(string? contentType, bool expected)
    {
        Assert.Equal(expected, ImageUploadPolicy.IsAllowedContentType(contentType));
    }
}
