using ImageMagick;
using Maroik.Core.Contract.Interfaces;
using Microsoft.Extensions.Logging;

namespace Maroik.Core.Service.Services;

/// <summary>ImageMagick-backed implementation of <see cref="IImageValidatorService"/>.</summary>
public class ImageValidatorService(ILogger<ImageValidatorService> logger) : IImageValidatorService
{
    /// <summary>Caps Magick.NET's process-wide decode limits once, before the first image is validated.</summary>
    static ImageValidatorService()
    {
        // These are process-global (Magick.NET has no per-instance limits), and this service is the
        // only Magick.NET consumer in the process. Bound decode work so a small "pixel bomb" — a
        // tiny file that declares enormous dimensions — can't exhaust memory. The caps sit far above
        // any real avatar or Summernote image.
        ResourceLimits.Width = 20_000;
        ResourceLimits.Height = 20_000;
        ResourceLimits.Area = 100_000_000;          // ~100 MP
        ResourceLimits.Memory = 256UL * 1024 * 1024; // 256 MiB
    }

    /// <summary>The 8-byte signature every PNG file starts with.</summary>
    private static readonly byte[] _pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    /// <summary>The SOI marker plus the first byte of the next marker, which every JPEG file starts with.</summary>
    private static readonly byte[] _jpegSignature = [0xFF, 0xD8, 0xFF];

    // How much of the file is looked at to recognize an SVG document: its <svg> element sits at the top
    // (after an optional BOM, XML declaration, DOCTYPE and comments).
    private const int SvgSniffLength = 2048;

    /// <inheritdoc />
    /// <remarks>
    /// Only JPEG and PNG (the formats <c>ImageUploadPolicy</c> allows) are decoded. The format is decided by
    /// the file's magic bytes BEFORE Magick.NET sees it, and Magick is then told to read exactly that
    /// format: left to itself it picks the decoder from the content, so a file named "x.png" holding an SVG
    /// or a PostScript document would be run through those (much larger) parsers just to be rejected
    /// afterward.
    /// </remarks>
    public bool IsValidImage(byte[] bytes)
    {
        // MagickImage's constructor throws ArgumentException (not MagickException) for an empty
        // stream, so this must be rejected before the decode attempt rather than via the catch below.
        if (bytes is not { Length: > 0 })
            return false;

        if (DetectFormat(bytes) is not { } format)
            return false;

        try
        {
            using var image = new MagickImage(new MemoryStream(bytes), new MagickReadSettings { Format = format });
            return true;
        }
        catch (MagickException e)
        {
            // The magic bytes matched but the decoder refused the content: a corrupt or crafted upload.
            logger.LogWarning(e, "Image with a {Format} signature could not be decoded and was rejected", format);
            return false;
        }
    }

    /// <inheritdoc />
    /// <remarks>A plain text sniff of the document start — it never hands the bytes to an image decoder.</remarks>
    public bool IsSvg(byte[] bytes)
    {
        if (bytes is not { Length: > 0 })
            return false;

        string head = System.Text.Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, SvgSniffLength));
        return head.Contains("<svg", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <c>AutoOrient</c> runs before <c>Strip</c>: once the EXIF Orientation tag is gone a viewer can no
    /// longer turn a sideways phone photo upright, so the rotation is baked into the pixels first.
    /// </remarks>
    public byte[] StripMetadata(byte[] bytes)
    {
        MagickFormat format = DetectFormat(bytes)
            ?? throw new ArgumentException("Only a JPEG or PNG image can be re-encoded.", nameof(bytes));

        using var image = new MagickImage(new MemoryStream(bytes), new MagickReadSettings { Format = format });
        image.AutoOrient();
        image.Strip();
        return image.ToByteArray(format);
    }

    /// <summary>The format named by the file's magic bytes: PNG, JPEG, or <see langword="null"/> for anything else.</summary>
    private static MagickFormat? DetectFormat(byte[] bytes)
    {
        if (bytes.AsSpan().StartsWith(_pngSignature))
            return MagickFormat.Png;
        if (bytes.AsSpan().StartsWith(_jpegSignature))
            return MagickFormat.Jpeg;
        return null;
    }
}
