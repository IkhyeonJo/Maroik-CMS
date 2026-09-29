namespace Maroik.FileStorage.Helpers;

/// <summary>
/// Confines a caller-supplied file path to the configured storage root. The callers
/// (BoardService / CalendarService / AttachmentContentService / ProfileService in Maroik.Core.Service,
/// through <c>FileClient</c>) only ever send server-generated relative paths that begin with <c>upload/</c>; this is the backstop that keeps
/// a malformed or hostile value from reading or writing anywhere else on the file system.
/// </summary>
internal static class StoragePath
{
    /// <summary>
    /// Resolves <paramref name="requestedPath"/> to an absolute path and returns it only if it stays
    /// inside <paramref name="uploadRoot"/> (the absolute path of the <c>upload</c> directory);
    /// otherwise returns <see langword="null"/>. A relative request is resolved against
    /// <paramref name="uploadRoot"/>'s parent, because the callers' relative paths already include
    /// the <c>upload/</c> segment.
    /// </summary>
    internal static string? ResolveWithinRoot(string? uploadRoot, string? requestedPath)
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
            return null;
        }

        return full.Equals(root, StringComparison.Ordinal)
               || full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? full
            : null;
    }
}
