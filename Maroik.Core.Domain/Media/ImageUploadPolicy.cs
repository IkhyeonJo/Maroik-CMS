namespace Maroik.Core.Domain.Media;

/// <summary>
/// The single source of truth for which image uploads the app accepts (avatars and
/// rich-text/summernote inline images). Controllers validate against it; the profile/editor
/// views serialize <see cref="AllowedContentTypes"/> so the client no longer hard-codes
/// "image/jpeg or image/png".
/// </summary>
public static class ImageUploadPolicy
{
    /// <summary>Accepted file extensions, lower-case, dot-prefixed.</summary>
    public static readonly IReadOnlyList<string> AllowedExtensions = [".jpg", ".jpeg", ".png"];

    /// <summary>Accepted MIME content types, lower-case.</summary>
    public static readonly IReadOnlyList<string> AllowedContentTypes = ["image/jpeg", "image/png"];

    /// <summary>Whether <paramref name="extension"/> (case-insensitive) is an accepted image extension.</summary>
    public static bool IsAllowedExtension(string? extension)
        => extension is not null && AllowedExtensions.Contains(extension.ToLowerInvariant());

    /// <summary>Whether <paramref name="contentType"/> (case-insensitive) is an accepted image content type.</summary>
    public static bool IsAllowedContentType(string? contentType)
        => contentType is not null && AllowedContentTypes.Contains(contentType.ToLowerInvariant());
}
