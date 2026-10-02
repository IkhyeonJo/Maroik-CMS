using ImageMagick;
using ImageMagick.Drawing;
using Maroik.Core.Service.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Maroik.Core.Service.Tests.Services;

/// <summary>Unit tests for <see cref="ImageValidatorService"/>.</summary>
public class ImageValidatorServiceTests
{
    /// <summary>Captures the log entries the system under test writes.</summary>
    private readonly FakeLogger<ImageValidatorService> _logger = new();
    /// <summary>The service under test.</summary>
    private readonly ImageValidatorService _sut;

    /// <summary>Creates the service under test over the capturing logger.</summary>
    public ImageValidatorServiceTests() => _sut = new ImageValidatorService(_logger);

    // Minimal valid 1x1 transparent PNG — a well-known fixture byte sequence, not a mock.
    private static readonly byte[] _validPngBytes =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, 0x89,
        0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41, 0x54,
        0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00, 0x05, 0x00, 0x01,
        0x0D, 0x0A, 0x2D, 0xB4,
        0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
    ];

    /// <summary>Verifies that a real, decodable PNG is accepted.</summary>
    [Fact]
    public void IsValidImage_ReturnsTrue_ForValidPng()
    {
        Assert.True(_sut.IsValidImage(_validPngBytes));
    }

    /// <summary>Verifies that non-image bytes are rejected rather than throwing.</summary>
    [Fact]
    public void IsValidImage_ReturnsFalse_ForGarbageBytes()
    {
        Assert.False(_sut.IsValidImage([0x00, 0x01, 0x02, 0x03]));
    }

    /// <summary>Verifies that empty input is rejected rather than throwing.</summary>
    [Fact]
    public void IsValidImage_ReturnsFalse_ForEmptyBytes()
    {
        Assert.False(_sut.IsValidImage([]));
    }

    /// <summary>Verifies that a non-SVG (PNG) image is not reported as SVG.</summary>
    [Fact]
    public void IsSvg_ReturnsFalse_ForPngImage()
    {
        Assert.False(_sut.IsSvg(_validPngBytes));
    }

    /// <summary>Verifies that garbage bytes are rejected rather than throwing.</summary>
    [Fact]
    public void IsSvg_ReturnsFalse_ForGarbageBytes()
    {
        Assert.False(_sut.IsSvg([0x00, 0x01, 0x02, 0x03]));
    }

    /// <summary>A real 2x2 red image encoded as <paramref name="format"/>.</summary>
    private static byte[] Encode(MagickFormat format)
    {
        using var image = new MagickImage(MagickColors.Red, 2, 2);
        image.Format = format;
        return image.ToByteArray();
    }

    /// <summary>A real, decodable JPEG is accepted.</summary>
    [Fact]
    public void IsValidImage_ReturnsTrue_ForValidJpeg()
    {
        Assert.True(_sut.IsValidImage(Encode(MagickFormat.Jpeg)));
    }

    /// <summary>
    /// Only JPEG and PNG are accepted. A perfectly decodable GIF is refused too — and, by the same
    /// mechanism, so is anything else Magick.NET could decode from content alone.
    /// </summary>
    [Fact]
    public void IsValidImage_ReturnsFalse_ForGif()
    {
        Assert.False(_sut.IsValidImage(Encode(MagickFormat.Gif)));
    }

    /// <summary>
    /// SVG and PostScript documents (which Magick.NET would happily render when given only their bytes)
    /// are rejected on their content without being decoded.
    /// </summary>
    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"><rect width=\"10\" height=\"10\"/></svg>")]
    [InlineData("%!PS-Adobe-3.0 EPSF-3.0\n%%BoundingBox: 0 0 10 10\nshowpage")]
    [InlineData("push graphic-context\nviewbox 0 0 10 10\nfill 'red'\ncircle 5,5 3,3\npop graphic-context")]
    public void IsValidImage_ReturnsFalse_ForNonRasterDocuments(string document)
    {
        Assert.False(_sut.IsValidImage(System.Text.Encoding.UTF8.GetBytes(document)));
    }

    /// <summary>A file that starts with the PNG signature but is not a decodable image is refused.</summary>
    [Fact]
    public void IsValidImage_ReturnsFalse_ForPngSignatureWithGarbageBody()
    {
        byte[] bytes = [.. _validPngBytes[..8], 0xDE, 0xAD, 0xBE, 0xEF];
        Assert.False(_sut.IsValidImage(bytes));
    }

    /// <summary>An SVG document is recognized as one, whether or not it carries an XML declaration, a BOM or odd casing.</summary>
    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>")]
    [InlineData("<?xml version=\"1.0\"?>\n<!-- c -->\n<SVG xmlns=\"http://www.w3.org/2000/svg\"></SVG>")]
    [InlineData("\uFEFF<svg></svg>")]
    public void IsSvg_ReturnsTrue_ForSvgDocuments(string document)
    {
        Assert.True(_sut.IsSvg(System.Text.Encoding.UTF8.GetBytes(document)));
    }

    /// <summary>An empty payload is not an SVG.</summary>
    [Fact]
    public void IsSvg_ReturnsFalse_ForEmptyBytes()
    {
        Assert.False(_sut.IsSvg([]));
    }

    /// <summary>
    /// A file that passes the magic-byte check but cannot be decoded (truncated / crafted) is a rejected
    /// upload: it is refused AND logged, so a probe of the decoder shows up in the logs.
    /// </summary>
    [Fact]
    public void IsValidImage_LogsAWarning_WhenAPngSignatureIsFollowedByUndecodableBytes()
    {
        byte[] truncated = [.. _validPngBytes.Take(20)];

        Assert.False(_sut.IsValidImage(truncated));

        FakeLogRecord record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Contains("could not be decoded", record.Message);
        Assert.NotNull(record.Exception);
    }

    /// <summary>A valid image and a plain non-image (no signature) are not decode failures and log nothing.</summary>
    [Fact]
    public void IsValidImage_LogsNothing_ForAValidImageAndForNonImageBytes()
    {
        Assert.True(_sut.IsValidImage(_validPngBytes));
        Assert.False(_sut.IsValidImage([0x00, 0x01, 0x02, 0x03]));

        Assert.Empty(_logger.Collector.GetSnapshot());
    }

    // -- StripMetadata ----------------------------------------------------------

    /// <summary>
    /// A <paramref name="width"/> x <paramref name="height"/> image whose left half is red and right half blue,
    /// carrying the given EXIF profile and encoded as <paramref name="format"/>.
    /// </summary>
    private static byte[] EncodeWithExif(MagickFormat format, uint width, uint height, IExifProfile exif)
    {
        using var image = new MagickImage(MagickColors.Red, width, height);
        new Drawables().FillColor(MagickColors.Blue).Rectangle(width / 2.0, 0, width, height).Draw(image);
        image.SetProfile(exif);
        // ImageMagick writes the Orientation tag from this property, not from the profile's own value.
        image.Orientation = (OrientationType)(exif.GetValue(ExifTag.Orientation)?.Value ?? 1);
        image.Format = format;
        return image.ToByteArray();
    }

    /// <summary>An EXIF profile holding a GPS position (Seoul), a camera model and the given orientation.</summary>
    private static ExifProfile GpsExif(ushort orientation = 1)
    {
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.GPSLatitudeRef, "N");
        exif.SetValue(ExifTag.GPSLatitude, [new Rational(37, 1), new Rational(33, 1), new Rational(59, 1)]);
        exif.SetValue(ExifTag.GPSLongitudeRef, "E");
        exif.SetValue(ExifTag.GPSLongitude, [new Rational(126, 1), new Rational(58, 1), new Rational(41, 1)]);
        exif.SetValue(ExifTag.Model, "SecretPhone 15");
        exif.SetValue(ExifTag.Orientation, orientation);
        return exif;
    }

    /// <summary>Decodes <paramref name="bytes"/> without any format hint.</summary>
    private static MagickImage Decode(byte[] bytes) => new(bytes);

    /// <summary>
    /// A phone JPEG with a GPS position is stored without its EXIF: neither the profile nor the coordinates
    /// or camera model survive, and it is still a JPEG.
    /// </summary>
    [Fact]
    public void StripMetadata_RemovesGpsExif_FromAJpeg()
    {
        byte[] original = EncodeWithExif(MagickFormat.Jpeg, 40, 20, GpsExif());
        using (var before = Decode(original))
            Assert.NotNull(before.GetExifProfile()); // the fixture really carries EXIF

        byte[] stripped = _sut.StripMetadata(original);

        using var after = Decode(stripped);
        Assert.Equal(MagickFormat.Jpeg, after.Format);
        Assert.Null(after.GetExifProfile());
        Assert.Equal(-1, System.Text.Encoding.ASCII.GetString(stripped).IndexOf("SecretPhone", StringComparison.Ordinal));
        Assert.Equal(-1, System.Text.Encoding.ASCII.GetString(stripped).IndexOf("Exif", StringComparison.Ordinal));
    }

    /// <summary>
    /// A photo taken sideways (EXIF Orientation 6 = "rotate 90° clockwise to display") is rotated into its
    /// display orientation before the tag is dropped, so it does not show up sideways once the EXIF is gone.
    /// </summary>
    [Fact]
    public void StripMetadata_AppliesTheExifOrientation_BeforeDroppingIt()
    {
        // 40x20, left half red / right half blue, to be shown rotated 90° clockwise.
        byte[] original = EncodeWithExif(MagickFormat.Jpeg, 40, 20, GpsExif(orientation: 6));
        using (var before = Decode(original))
            Assert.Equal(OrientationType.RightTop, before.Orientation); // the fixture really is tagged sideways

        using var after = Decode(_sut.StripMetadata(original));

        Assert.Equal(20u, after.Width);
        Assert.Equal(40u, after.Height);
        Assert.Equal(OrientationType.Undefined, after.Orientation);
        // Rotated clockwise, the original left (red) half is now the top half and the right (blue) half the bottom.
        using var pixels = after.GetPixels();
        IMagickColor<byte> top = pixels.GetPixel(10, 5).ToColor()!;
        IMagickColor<byte> bottom = pixels.GetPixel(10, 35).ToColor()!;
        Assert.True(top.R > 200 && top.B < 60, $"top should be red, was {top}");
        Assert.True(bottom.B > 200 && bottom.R < 60, $"bottom should be blue, was {bottom}");
    }

    /// <summary>A PNG stays a PNG of the same size, with its EXIF and text chunks removed.</summary>
    [Fact]
    public void StripMetadata_KeepsAPngAPng_WithoutMetadata()
    {
        byte[] original;
        using (var image = new MagickImage(MagickColors.Red, 8, 6))
        {
            image.SetProfile(GpsExif());
            image.SetAttribute("Comment", "taken at home");
            image.Format = MagickFormat.Png;
            original = image.ToByteArray();
        }

        byte[] stripped = _sut.StripMetadata(original);

        using var after = Decode(stripped);
        Assert.Equal(MagickFormat.Png, after.Format);
        Assert.Equal(8u, after.Width);
        Assert.Equal(6u, after.Height);
        Assert.Null(after.GetExifProfile());
        Assert.Null(after.GetAttribute("Comment"));
        Assert.True(_sut.IsValidImage(stripped));
    }

    /// <summary>Only an image that passed <see cref="ImageValidatorService.IsValidImage"/> may be re-encoded.</summary>
    [Fact]
    public void StripMetadata_Throws_ForBytesThatAreNotAJpegOrPng()
    {
        Assert.Throws<ArgumentException>(() => _sut.StripMetadata(Encode(MagickFormat.Gif)));
    }
}
