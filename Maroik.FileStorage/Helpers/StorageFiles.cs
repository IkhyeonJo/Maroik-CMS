namespace Maroik.FileStorage.Helpers;

/// <summary>
/// The only code in Maroik.FileStorage that touches the file system. Every member takes a <see cref="StoragePath"/>, never a
/// string, so whatever it reads or writes was confined to the storage root first (pinned by
/// <c>FileSystemAccessArchitectureTests</c>: no other file of this service names <c>File</c>, <c>Directory</c> or
/// <c>FileStream</c>, and no member here takes a <c>string</c>).
/// </summary>
internal static class StorageFiles
{
    /// <summary>Whether <paramref name="file"/> exists.</summary>
    internal static bool Exists(StoragePath file) => File.Exists(file.FullPath);

    /// <summary>
    /// Opens <paramref name="file"/> for a streamed download (asynchronous, sequential, shared for reading); the caller owns
    /// and disposes the stream.
    /// </summary>
    internal static FileStream OpenRead(StoragePath file) =>
        new(file.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

    /// <summary>Creates <paramref name="directory"/> (and its parents) unless it exists.</summary>
    internal static void CreateDirectory(StoragePath directory) => Directory.CreateDirectory(directory.FullPath);

    /// <summary>
    /// Creates <paramref name="file"/>, failing if it already exists (never overwrites); the caller owns and disposes the
    /// stream.
    /// </summary>
    internal static FileStream CreateNew(StoragePath file) => new(file.FullPath, FileMode.CreateNew);

    /// <summary>
    /// Sets mode 644 on <paramref name="file"/> so the process serving downloads can read it regardless of the container's
    /// umask. Uses the framework API rather than shelling out to <c>/bin/chmod</c>; a no-op on Windows (dev only — the
    /// deployment target is Linux).
    /// </summary>
    internal static void MakeReadable(StoragePath file)
    {
        if (OperatingSystem.IsWindows())
            return;

        File.SetUnixFileMode(
            file.FullPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite |
            UnixFileMode.GroupRead | UnixFileMode.OtherRead);
    }
}
