namespace Maroik.Core.Contract.Misc.Messaging;

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
}
