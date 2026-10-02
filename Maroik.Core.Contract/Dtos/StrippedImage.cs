namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// An uploaded image re-encoded without its metadata (see <c>IImageValidatorService.StripMetadata</c>), with
/// the file extension and content type of the format it actually is — decided by its content, not by the
/// name or type the upload claimed.
/// </summary>
/// <param name="Bytes">The re-encoded image.</param>
/// <param name="Extension">The extension to store it under (".jpg" or ".png").</param>
/// <param name="ContentType">Its content type ("image/jpeg" or "image/png").</param>
public sealed record StrippedImage(byte[] Bytes, string Extension, string ContentType);
