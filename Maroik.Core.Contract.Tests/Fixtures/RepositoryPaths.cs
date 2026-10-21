namespace Maroik.Core.Contract.Tests.Fixtures;

/// <summary>
/// The repository the solution-wide architecture tests read (project files, <c>Maroik.slnx</c>, sources), found once by walking
/// up from the test output to the folder holding <c>Maroik.slnx</c>.
/// </summary>
internal static class RepositoryPaths
{
    /// <summary>The repository root: the folder holding <c>Maroik.slnx</c>.</summary>
    public static string Root { get; } = FindRoot();

    /// <summary>True when <paramref name="path"/> has a directory segment equal to <paramref name="segment"/> (case-insensitive).</summary>
    public static bool HasSegment(string path, string segment) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Contains(segment, StringComparer.OrdinalIgnoreCase);

    /// <summary>Walks up from the test binary's folder to the directory holding <c>Maroik.slnx</c>.</summary>
    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Maroik.slnx")))
            dir = dir.Parent;
        return dir?.FullName
               ?? throw new InvalidOperationException("Could not locate the repository root (Maroik.slnx) from " + AppContext.BaseDirectory);
    }
}
