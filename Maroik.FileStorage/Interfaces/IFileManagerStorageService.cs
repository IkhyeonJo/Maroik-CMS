namespace Maroik.FileStorage.Interfaces;

/// <summary>
/// The File Manager's large-file flow on disk (Implementation note [A]), kept apart from <see cref="IFileValidationService"/>, which
/// serves the small attachments: chunks are appended to a staging file, a completed upload is scanned from disk and moved to
/// <c>upload/FileManager/{GUID}</c>, an abandoned one is dropped, and a stored File Manager file can be deleted (the one exception to
/// the attachments' "no delete" rule, P4). A refusal is returned as one of the <c>FileStorageRefusals</c> texts.
/// </summary>
public interface IFileManagerStorageService
{
    /// <summary>
    /// Appends <paramref name="chunk"/> to the staging file of <paramref name="uploadId"/> when <paramref name="offset"/> equals the
    /// length received so far. Returns no error and the new length, or the refusal and the length received so far.
    /// </summary>
    Task<(string? Error, long Length)> AppendChunkAsync(string? uploadId, long offset, Stream chunk, CancellationToken ct = default);

    /// <summary>
    /// Completes the upload: checks the staging file is <paramref name="expectedSize"/> bytes, scans it with ClamAV and moves it to
    /// <c>upload/FileManager/{uploadId}</c>. Returns null when stored, or the refusal (the staging file is then deleted).
    /// </summary>
    Task<string?> CompleteAsync(string? uploadId, long expectedSize, CancellationToken ct = default);

    /// <summary>Deletes the staging file of <paramref name="uploadId"/> if there is one; false for an id that is not a GUID.</summary>
    bool Abort(string? uploadId);

    /// <summary>Deletes the File Manager file at <paramref name="filePath"/>; returns null when deleted, or the refusal.</summary>
    string? Delete(string? filePath);
}
