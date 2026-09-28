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
}
