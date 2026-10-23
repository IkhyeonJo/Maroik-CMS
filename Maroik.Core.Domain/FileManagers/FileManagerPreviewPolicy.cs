namespace Maroik.Core.Domain.FileManagers;

/// <summary>
/// Which files the File Manager previews (F9): png, jpg, gif and webp images of up to <see cref="MaxPreviewBytes"/>. Everything else —
/// SVG included, since it can run script on the site's origin — is download-only. The extension alone is not trusted: the preview is
/// answered only when the file's leading bytes prove one of the four formats, with the Content-Type of that proven format.
/// </summary>
public static class FileManagerPreviewPolicy
{
    /// <summary>The largest file that is previewed, 100 MB (the preview is fetched whole and shown through a blob URL).</summary>
    public const long MaxPreviewBytes = 100L * 1024 * 1024;

    /// <summary>The number of leading bytes <see cref="ContentTypeOf"/> needs to tell the formats apart.</summary>
    public const int HeaderBytes = 12;

    /// <summary>The previewable extensions (lower-case).</summary>
    private static readonly HashSet<string> _extensions = new(StringComparer.Ordinal) { ".png", ".jpg", ".jpeg", ".gif", ".webp" };

    /// <summary>Whether a file named <paramref name="name"/> of <paramref name="size"/> bytes may be offered a preview.</summary>
    public static bool IsPreviewable(string name, long size) =>
        size <= MaxPreviewBytes && _extensions.Contains(Path.GetExtension(name).ToLowerInvariant());

    /// <summary>The image Content-Type the leading bytes prove, or null when they prove none of the previewable formats.</summary>
    public static string? ContentTypeOf(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
            return "image/png";
        if (header.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
            return "image/jpeg";
        if (header.StartsWith("GIF87a"u8) || header.StartsWith("GIF89a"u8))
            return "image/gif";
        if (header.Length >= HeaderBytes && header.StartsWith("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
            return "image/webp";
        return null;
    }
}
