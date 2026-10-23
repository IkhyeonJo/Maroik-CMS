namespace Maroik.Core.Domain.FileManagers;

/// <summary>
/// What an upload does when a finished file of the same name is already in the folder (F2-C, Windows Explorer behaviour). "Skip" is
/// the client not uploading the file at all, so it has no value here.
/// </summary>
public enum FileManagerConflictChoice
{
    /// <summary>Not decided yet: the upload does not start, and the user is asked.</summary>
    Ask,

    /// <summary>Upload under the same name; the existing file is deleted once the new one is stored (no versioning).</summary>
    Overwrite,

    /// <summary>Upload under the first free "name (n).ext" (<see cref="FileManagerNamePolicy.KeepBothName"/>).</summary>
    KeepBoth
}
