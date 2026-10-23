namespace Maroik.Core.Contract.Messages;

/// <summary>
/// The plain-text refusal reasons the file-storage service puts in the body of a rejected upload.
/// Shared by the service (which writes them) and <c>FileClient</c> (which maps them back to a
/// <c>FileUploadResult</c>), so the two cannot drift apart.
/// </summary>
public static class FileStorageRefusals
{
    /// <summary>The virus scan reported a detection.</summary>
    public const string Infected = "File may be infected with a virus.";

    /// <summary>The virus scan could not be completed.</summary>
    public const string ScanUnavailable = "The file could not be scanned for viruses. Please try again later.";

    /// <summary>A File Manager upload id that is not a GUID, or an upload that cannot be stored (its file already exists).</summary>
    public const string InvalidUpload = "The upload is not valid.";

    /// <summary>A File Manager chunk whose offset is not the length received so far.</summary>
    public const string OffsetMismatch = "The chunk does not continue the upload.";

    /// <summary>A completed File Manager upload whose received size differs from the declared size.</summary>
    public const string SizeMismatch = "The uploaded size does not match the declared size.";

    /// <summary>A delete that names anything but a File Manager file (the attachments have no delete API).</summary>
    public const string NotAFileManagerFile = "Only a File Manager file can be deleted.";

    /// <summary>A delete of a File Manager file that does not exist.</summary>
    public const string FileNotFound = "File not found";
}
