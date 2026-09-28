using Maroik.Core.Contract.Dtos;

namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Shared attachment and rich-text-content logic used by any feature service that manages
/// posts with zip attachments and Summernote-authored HTML (e.g. calendar events, board posts):
/// attachment validation, Summernote inline-image upload, file download, and HTML
/// sanitization/encryption for storage vs. decryption/embedding for display.
/// </summary>
public interface IAttachmentContentService
{
    /// <summary>
    /// Validates a zip attachment against the configured extension and size policy.
    /// Returns <see langword="null"/> when the file passes validation (or when no file is provided).
    /// </summary>
    ServiceResult? ValidateAttachedFile(AttachedFileDto? attachedFile);

    /// <summary>Validates, uploads, and re-downloads a Summernote inline image for preview.</summary>
    Task<SummernoteUploadResult> UploadSummernoteImageAsync(AttachedFileDto file, string area, string subArea, CancellationToken ct = default);

    /// <summary>
    /// Downloads a file from file storage. Returns <see langword="null"/> (and logs) on failure
    /// instead of throwing, so callers can treat a missing/unreachable file as absent content.
    /// </summary>
    Task<byte[]?> DownloadFileAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// Downloads and embeds any RSA-encrypted inline images in <paramref name="html"/> as base64
    /// data for display, re-encrypting each image's storage path in its <c>alt</c> attribute.
    /// </summary>
    Task<(string Html, bool HasImages)> PrepareHtmlForDisplayAsync(string html, CancellationToken ct = default);

    /// <summary>
    /// Sanitizes <paramref name="html"/> and decrypts the RSA-encrypted storage path in each
    /// inline image's <c>alt</c> attribute, ready to persist.
    /// </summary>
    string SanitizeAndDecryptContent(string html);

    /// <summary>Sanitizes HTML with no inline-image handling (for content with no embedded images, e.g. comments).</summary>
    string SanitizeContent(string html);
}
