namespace Maroik.Core.Domain.Media;

/// <summary>
/// The single source of truth for which zip-attachment uploads the app accepts (Board posts,
/// Calendar events). Mirrors <see cref="ImageUploadPolicy"/>'s pattern: the size cap is
/// configured at runtime (see <c>ServerSetting.MaxAttachedFileSizeBytes</c>), so the caller
/// supplies it and this class owns only the rule.
/// </summary>
public static class AttachmentUploadPolicy
{
    /// <summary>Accepted file extensions, lower-case, dot-prefixed.</summary>
    public static readonly IReadOnlyList<string> AllowedExtensions = [".zip"];

    /// <summary>Whether <paramref name="extension"/> (case-insensitive) is an accepted attachment extension.</summary>
    public static bool IsAllowedExtension(string? extension)
        => extension is not null && AllowedExtensions.Contains(extension.ToLowerInvariant());

    /// <summary>Whether <paramref name="sizeBytes"/> is a non-empty upload within <paramref name="maxBytes"/>.</summary>
    public static bool IsValidSize(long sizeBytes, long maxBytes)
        => sizeBytes > 0 && sizeBytes <= maxBytes;
}
