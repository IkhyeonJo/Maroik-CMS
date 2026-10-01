using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Settings;
using Maroik.Core.Domain.Media;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Maroik.Core.Service.Services;

/// <summary>
/// Implementation of <see cref="IAttachmentContentService"/>. Files are virus-scanned by
/// ClamAV and image-validated before upload, and stored in/retrieved from the file-storage
/// microservice (<see cref="IFileClient"/>).
/// </summary>
public class AttachmentContentService(
    IFileClient fileClient,
    IRsaService rsa,
    IImageValidatorService imageValidator,
    IHtmlContentSanitizerService htmlSanitizer,
    IHtmlParserService htmlParser,
    IOptions<ServerSetting> settings,
    ILogger<AttachmentContentService> logger) : IAttachmentContentService
{
    /// <inheritdoc />
    public ServiceResult? ValidateAttachedFile(AttachedFileDto? attachedFile)
    {
        if (attachedFile == null) return null;
        if (!AttachmentUploadPolicy.IsAllowedExtension(Path.GetExtension(attachedFile.FileName)))
            return ServiceResult.Validation("Attachment.ExtensionNotAllowed", "Only zip extension allowed.");
        long maxBytes = settings.Value.MaxAttachedFileSizeBytes;
        return !AttachmentUploadPolicy.IsValidSize(attachedFile.Size, maxBytes) ?
            // Stable, value-independent key (same one ManagementController/ForumController/
            // CalendarController already use for the avatar/Summernote-image size errors) so the
            // resx lookup still matches when MaxAttachedFileSizeBytes is configured away from
            // 10MB. The MB figure travels with the result via ErrorArgs, so every caller can just
            // do `_localizer[result.ErrorKey, result.ErrorArgs]` instead of separately recomputing
            // maxBytes/(1024*1024) at the call site.
            ServiceResult.Validation("Attachment.TooLarge", "File Size must be smaller than {0}MB.", maxBytes / (1024 * 1024)) : null;
    }

    /// <inheritdoc />
    public async Task<SummernoteUploadResult> UploadSummernoteImageAsync(AttachedFileDto file, string area, string subArea, CancellationToken ct = default)
    {
        string ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        // Same allow-list gate the avatar upload path enforces (ImageUploadPolicy is the single
        // source of truth): reject anything outside jpg/png by extension or declared content type,
        // and reject SVG outright. IsValidImage itself only accepts real JPEG/PNG content (by magic
        // bytes, and Magick is told to read exactly that format), so the label on the file never
        // decides which decoder the bytes are fed to.
        if (!ImageUploadPolicy.IsAllowedExtension(ext) || !ImageUploadPolicy.IsAllowedContentType(file.ContentType))
            return SummernoteUploadResult.Fail("Invalid image file.");

        if (imageValidator.IsSvg(file.Bytes) || !imageValidator.IsValidImage(file.Bytes))
            return SummernoteUploadResult.Fail("Invalid image file.");

        // Use a GUID-based file name to avoid collisions and prevent path traversal via the original name.
        string imageFile = $"{Guid.NewGuid():N}{ext}";
        // A storage key, not a local path: always "/"-separated, whatever OS this runs on.
        string filePath = $"upload/{area}/{subArea}/summernote/images/{imageFile}";

        bool uploaded = await fileClient.UploadAsync(file.Bytes, file.ContentType, filePath, settings.Value.FileStorageBaseUrl ?? "", ct);
        if (!uploaded)
            return SummernoteUploadResult.Fail("Input is invalid");

        try
        {
            byte[] fileBytes = await fileClient.DownloadAsync(filePath, settings.Value.FileStorageBaseUrl ?? "", ct);
            return SummernoteUploadResult.Ok(fileBytes, file.ContentType, imageFile, filePath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download file {FilePath} immediately after upload", filePath);
            return SummernoteUploadResult.Fail("Input is invalid");
        }
    }

    /// <inheritdoc />
    public async Task<byte[]?> DownloadFileAsync(string filePath, CancellationToken ct = default)
    {
        try
        {
            return await fileClient.DownloadAsync(filePath, settings.Value.FileStorageBaseUrl ?? "", ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download file {FilePath}", filePath);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<Stream?> OpenFileAsync(string filePath, CancellationToken ct = default)
    {
        try
        {
            return await fileClient.OpenReadAsync(filePath, settings.Value.FileStorageBaseUrl ?? "", ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to open file {FilePath}", filePath);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<(string Html, bool HasImages)> PrepareHtmlForDisplayAsync(string html, CancellationToken ct = default)
    {
        return await htmlParser.TransformImageAttributesAsync(html, async (alt, token) =>
        {
            try
            {
                byte[] fileData = await fileClient.DownloadAsync(alt, settings.Value.FileStorageBaseUrl ?? "", token);
                return new HtmlImgPatch(
                    NewAlt: rsa.Encrypt(alt),
                    DataFile: Convert.ToBase64String(fileData),
                    DataContentType: GetImageContentType(alt));
            }
            catch (Exception ex)
            {
                // A missing/unreachable image must not fail the whole post/event render — drop just
                // this <img> tag rather than leave its patch untouched: a null patch here would leave
                // the plaintext storage path sitting in `alt` (it's only ever re-encrypted by the
                // NewAlt patch above), exposing internal storage layout to the client. Remove: true
                // is the same graceful-degradation contract as DownloadFileAsync, minus the leak.
                logger.LogError(ex, "Failed to download image {FilePath} while preparing HTML for display", alt);
                return new HtmlImgPatch(Remove: true);
            }
        }, ct);
    }

    /// <inheritdoc />
    public string SanitizeAndDecryptContent(string html)
    {
        string sanitized = htmlSanitizer.Sanitize(html);
        return htmlParser.TransformImageAttributes(sanitized, alt =>
        {
            // Nothing to resolve — leave a bare <img> as-is (inert on display: PrepareHtmlForDisplayAsync
            // can't fetch an empty path, and the CSP blocks any cross-origin src).
            if (string.IsNullOrEmpty(alt))
                return null;

            string? decrypted = TryDecryptImageAlt(alt);

            // A genuine Summernote image always carries the server-issued RSA-OAEP token in alt (set
            // by UploadSummernoteImageAsync and re-applied by PrepareHtmlForDisplayAsync on every
            // edit round-trip). An alt that does not decrypt is a reference the editor never produced
            // — e.g. a hand-crafted plaintext storage path posted straight to the write endpoint.
            // Drop the whole <img> rather than persist it, so it cannot drive a server-side file
            // fetch when the post is later rendered.
            return decrypted != null
                ? new HtmlImgPatch(NewAlt: decrypted)
                : new HtmlImgPatch(Remove: true);
        });
    }

    /// <summary>
    /// An <c>&lt;img&gt;</c>'s <c>alt</c> is normally the RSA-OAEP token this service round-trips
    /// back to the real storage path. Returns the decrypted path, or <see langword="null"/> when
    /// <paramref name="alt"/> is empty or not a valid token (<see cref="IRsaService.Decrypt"/> throws —
    /// a CryptographicException for a malformed or undecryptable value; the failure is logged at
    /// Warning) — the guarded-decrypt shape the account-token flows already use, so a forged post
    /// can't turn the save into an unhandled 500.
    /// </summary>
    private string? TryDecryptImageAlt(string alt)
    {
        if (string.IsNullOrEmpty(alt)) return null;
        try
        {
            return rsa.Decrypt(alt);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ignoring an image alt that is not a decryptable storage token");
            return null;
        }
    }

    /// <inheritdoc />
    public string SanitizeContent(string html) => htmlSanitizer.Sanitize(html);

    /// <summary>Returns the MIME content type for Summernote-uploaded images by file extension.</summary>
    private static string GetImageContentType(string filePath) =>
        Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
}
