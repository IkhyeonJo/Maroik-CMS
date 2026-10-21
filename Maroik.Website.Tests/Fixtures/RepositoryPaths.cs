namespace Maroik.Website.Tests.Fixtures;

/// <summary>
/// Folders of the repository the tests read from (the views, the resx files, the init script), found once by walking up from
/// the test output to the folder holding <c>Maroik.slnx</c>.
/// </summary>
internal static class RepositoryPaths
{
    /// <summary>The repository root: the folder holding <c>Maroik.slnx</c>.</summary>
    public static string Root { get; } = FindRoot();

    /// <summary>The <c>Maroik.Website</c> project folder.</summary>
    public static string Website => Path.Combine(Root, "Maroik.Website");

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
