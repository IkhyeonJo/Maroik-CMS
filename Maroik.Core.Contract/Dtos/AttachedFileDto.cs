namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// In-memory file descriptor passed from the controller to the service layer
/// when a file is uploaded along with a form submission (e.g. board post attachment,
/// calendar event attachment, Summernote inline image).
/// </summary>
public class AttachedFileDto
{
    /// <summary>Raw file bytes read from the uploaded stream.</summary>
    public byte[] Bytes { get; init; } = [];

    /// <summary>MIME content type detected from the upload (e.g. "image/png", "application/zip").</summary>
    public string ContentType { get; init; } = "";

    /// <summary>Sanitised original file name provided by the client.</summary>
    public string FileName { get; init; } = "";

    /// <summary>File size in bytes.</summary>
    public long Size { get; init; }
}
