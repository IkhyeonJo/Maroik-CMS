namespace Maroik.Core.Contract.Dtos;

/// <summary>A File Manager image ready to preview: its bytes and the Content-Type its magic bytes proved (F9, S6).</summary>
/// <param name="Bytes">The image's bytes (at most 100 MB).</param>
/// <param name="ContentType">The image Content-Type the server sets.</param>
public sealed record FileManagerPreview(byte[] Bytes, string ContentType);
