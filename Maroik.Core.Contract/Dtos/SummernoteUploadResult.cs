// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Result returned by the Summernote image-upload operation.
/// Summernote is the WYSIWYG HTML editor used in board posts and calendar events.
/// Use the static factory methods <see cref="Ok"/> and <see cref="Fail"/> to create instances.
/// </summary>
public class SummernoteUploadResult
{
    /// <summary>True when the file was validated and stored successfully.</summary>
    public bool Success { get; private init; }

    /// <summary>Raw file bytes, used to serve the image back to the editor on success.</summary>
    public byte[]? FileBytes { get; private init; }

    /// <summary>MIME content type of the uploaded file (e.g. "image/png").</summary>
    public string? ContentType { get; private init; }

    /// <summary>Sanitised original file name.</summary>
    public string? FileName { get; private init; }

    /// <summary>Server-side storage path where the file was saved.</summary>
    public string? FilePath { get; private init; }

    /// <summary>Localization key describing the error (null on success).</summary>
    public string? ErrorKey { get; private init; }

    /// <summary>Creates a successful upload result with the stored file data.</summary>
    public static SummernoteUploadResult Ok(byte[] fileBytes, string contentType, string fileName, string filePath)
        => new() { Success = true, FileBytes = fileBytes, ContentType = contentType, FileName = fileName, FilePath = filePath };

    /// <summary>Creates a failed upload result with the given error key.</summary>
    public static SummernoteUploadResult Fail(string errorKey)
        => new() { Success = false, ErrorKey = errorKey };
}
