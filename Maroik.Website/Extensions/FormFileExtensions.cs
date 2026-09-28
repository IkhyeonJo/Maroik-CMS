using Maroik.Core.Contract.Dtos;

namespace Maroik.Website.Extensions;

/// <summary>
/// Extension methods for <see cref="IFormFile"/> to simplify reading uploaded files
/// into service-layer data transfer objects.
/// </summary>
public static class FormFileExtensions
{
    extension(IFormFile? file)
    {
        /// <summary>
        /// Reads the uploaded file into an <see cref="AttachedFileDto"/>.
        /// Returns <see langword="null"/> when <paramref name="file"/> is <see langword="null"/>.
        /// <para>
        /// An upload larger than <paramref name="maxBytes"/> is <b>not</b> read into memory: a
        /// body-less descriptor carrying the real <see cref="AttachedFileDto.Size"/> (and name /
        /// content type) is returned so the service layer's size check still reports the usual
        /// <c>Attachment.TooLarge</c> result — without first buffering (and then doubling, via
        /// <c>ToArray</c>) a payload that is only going to be rejected.
        /// <see cref="IFormFile.Length"/> is the multipart section's declared length and is always
        /// known here, so this branch never touches the stream.
        /// </para>
        /// </summary>
        /// <param name="maxBytes">Upload size cap — pass <c>ServerSetting.MaxAttachedFileSizeBytes</c>.</param>
        /// <param name="ct">Cancellation token.</param>
        public async Task<AttachedFileDto?> ToAttachedFileInfoAsync(long maxBytes, CancellationToken ct = default)
        {
            if (file == null) return null;

            if (file.Length > maxBytes)
            {
                return new AttachedFileDto
                {
                    Bytes = [],
                    ContentType = file.ContentType,
                    FileName = file.FileName,
                    Size = file.Length
                };
            }

            using MemoryStream ms = new();
            await file.CopyToAsync(ms, ct);
            return new AttachedFileDto
            {
                Bytes = ms.ToArray(),
                ContentType = file.ContentType,
                FileName = file.FileName,
                Size = file.Length
            };
        }
    }
}
