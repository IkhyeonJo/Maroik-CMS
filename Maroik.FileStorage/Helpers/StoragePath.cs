namespace Maroik.FileStorage.Helpers;

/// <summary>
/// A path confined to the configured storage root. The callers
/// (BoardService / CalendarService / AttachmentContentService / ProfileService in Maroik.Core.Service,
/// through <c>FileClient</c>) only ever send server-generated relative paths that begin with <c>upload/</c>; this is the backstop that keeps
/// a malformed or hostile value from reading or writing anywhere else on the file system.
/// <para>
/// The constructor is private: a value exists only once <see cref="ResolveWithinRoot"/> (or <see cref="InDirectory"/> on a
/// value that did) has checked it, and <see cref="StorageFiles"/> — the only code in this service that touches the file
/// system — takes nothing else. A string that skipped the check therefore cannot reach a file at all; the compiler refuses it
/// (and <c>FileSystemAccessArchitectureTests</c> keeps <c>File</c> / <c>FileStream</c> out of the rest of the service).
/// </para>
/// </summary>
internal sealed class StoragePath
{
    /// <summary>Wraps a path the factory methods below have already confined.</summary>
    private StoragePath(string fullPath) => FullPath = fullPath;

    /// <summary>The absolute path, inside the storage root.</summary>
    internal string FullPath { get; }

    /// <summary>
    /// Resolves <paramref name="requestedPath"/> to an absolute path and returns it only if it stays
    /// inside <paramref name="uploadRoot"/> (the absolute path of the <c>upload</c> directory);
    /// otherwise returns <see langword="null"/>. A relative request is resolved against
    /// <paramref name="uploadRoot"/>'s parent, because the callers' relative paths already include
    /// the <c>upload/</c> segment.
    /// </summary>
    internal static StoragePath? ResolveWithinRoot(string? uploadRoot, string? requestedPath)
    {
        if (string.IsNullOrWhiteSpace(uploadRoot) || string.IsNullOrWhiteSpace(requestedPath))
            return null;

        string root;
        string full;
        try
        {
            root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(uploadRoot));
            string resolveBase = Path.GetDirectoryName(root) ?? root;
            // GetFullPath collapses "." / ".." and normalizes separators. An absolute requestedPath
            // ignores resolveBase; a relative one (the real callers' case) resolves under it.
            full = Path.GetFullPath(requestedPath, resolveBase);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // Expected input, not a fault: a path that cannot be resolved is refused like one outside the root (the caller answers it).
            return null;
        }

        return full.Equals(root, StringComparison.Ordinal)
               || full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? new StoragePath(full)
            : null;
    }

    /// <summary>
    /// The file <paramref name="fileName"/> directly inside this directory, or <see langword="null"/> when the name is not a
    /// plain file name: blank, <c>.</c> / <c>..</c> (this directory or its parent), or carrying a directory part. Joining only a
    /// plain name keeps the result inside this directory, so it stays confined like the directory itself.
    /// </summary>
    internal StoragePath? InDirectory(string? fileName) =>
        string.IsNullOrWhiteSpace(fileName) || fileName is "." or ".." || Path.GetFileName(fileName) != fileName
            ? null
            : new StoragePath(Path.Combine(FullPath, fileName));
}
