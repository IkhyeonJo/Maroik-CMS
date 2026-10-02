namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Validates raw image bytes without coupling callers to a specific imaging library.
/// </summary>
public interface IImageValidatorService
{
    /// <summary>
    /// Returns true when <paramref name="bytes"/> are a JPEG or PNG image (recognized by content, not by any
    /// file name) that can actually be decoded. Every other format — GIF, SVG, PostScript, ... — is false.
    /// </summary>
    bool IsValidImage(byte[] bytes);

    /// <summary>Returns true when <paramref name="bytes"/> look like an SVG document (a text check; nothing is decoded).</summary>
    bool IsSvg(byte[] bytes);

    /// <summary>
    /// Re-encodes an image that <see cref="IsValidImage"/> accepted into the same format (JPEG or PNG) without
    /// its metadata — EXIF (GPS position, camera, capture time), XMP, IPTC, comments — after first turning
    /// it upright by its EXIF orientation, so the pixels still display the way they did with the tag.
    /// Throws <see cref="ArgumentException"/> for bytes that are not a JPEG or PNG.
    /// </summary>
    byte[] StripMetadata(byte[] bytes);
}
