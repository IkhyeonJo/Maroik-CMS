namespace Maroik.Core.Contract.Misc.Enums;

/// <summary>
/// Outcome of an upload to the file-storage service (see <c>IFileClient.UploadWithResultAsync</c>).
/// Only <see cref="Stored"/> means the file is on disk; the other values are all refusals, kept apart
/// so a caller can tell a virus detection from a scanner outage from any other failure.
/// </summary>
public enum FileUploadResult
{
    /// <summary>The storage service accepted and stored the file.</summary>
    Stored = 0,

    /// <summary>The storage service's virus scan flagged the file.</summary>
    Infected,

    /// <summary>The virus scan could not be completed (scanner unreachable, timed out, or gave no verdict), so the file was refused.</summary>
    ScanUnavailable,

    /// <summary>Any other failure: empty input, an unsafe path, a rejected format/size, or a transport error.</summary>
    Failed
}
