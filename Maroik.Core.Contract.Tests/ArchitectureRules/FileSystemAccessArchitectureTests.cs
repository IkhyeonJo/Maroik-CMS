using Maroik.Core.Contract.Tests.Fixtures;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Maroik.Core.Contract.Tests.ArchitectureRules;

/// <summary>
/// Path traversal has no static analyzer since SecurityCodeScan (SCS0018) left the build, and CA3003 cannot be told about the
/// project's own validator. This class keeps the file system in view instead: production code touches it (<c>File</c>,
/// <c>Directory</c>, <c>FileStream</c>, <c>FileInfo</c>, <c>DirectoryInfo</c>) only in the files listed in
/// <see cref="_allowed"/>, each with the reason its paths are safe, so a new file-system call is a decision someone writes
/// down rather than a path nobody checked. In <c>Maroik.FileStorage</c>, the service that stores user uploads, a listed file
/// must also confine its paths with <c>StoragePath.ResolveWithinRoot</c> (pinned by <c>StoragePathTests</c>).
/// </summary>
public class FileSystemAccessArchitectureTests
{
    /// <summary>The file-system types whose use is confined to <see cref="_allowed"/>.</summary>
    private static readonly HashSet<string> _fileSystemTypes = ["File", "Directory", "FileStream", "FileInfo", "DirectoryInfo"];

    /// <summary>The project whose file-system access must go through <c>StoragePath.ResolveWithinRoot</c>.</summary>
    private const string FileStorageProject = "Maroik.FileStorage";

    /// <summary>The production files that may touch the file system, and why their paths cannot be steered outside their place.</summary>
    private static readonly Dictionary<string, string> _allowed = new(StringComparer.Ordinal)
    {
        ["Maroik.FileStorage/Controllers/FileController.cs"] =
            "downloads only a path StoragePath.ResolveWithinRoot confined to the storage root",
        ["Maroik.FileStorage/Services/FileValidationService.cs"] =
            "writes under a directory StoragePath.ResolveWithinRoot confined, with Path.GetFileName of the upload's name",
        ["Maroik.Website/Middlewares/AvatarCacheMiddleware.cs"] =
            "writes a fixed wwwroot directory, under a file name that must be a GUID with an allowed image extension",
        ["Maroik.Core.Service/Services/DashboardService.cs"] =
            "reads the two server-resource report files whose paths are server configuration, never request input",
        ["Maroik.Website/Program.cs"] = "creates the Data Protection key-ring directory named by server configuration",
    };

    [Fact]
    public void OnlyTheListedFiles_TouchTheFileSystem()
    {
        string[] unlisted =
        [
            .. ProductionSources().Where(s => FileSystemUses(s.Tree).Any() && !_allowed.ContainsKey(s.Path))
                .Select(s => $"{s.Path}:{string.Join(",", FileSystemUses(s.Tree))} touches the file system but is not in the list"),
        ];

        Assert.True(unlisted.Length == 0, string.Join(Environment.NewLine, unlisted));
    }

    [Fact]
    public void EveryListedFile_StillTouchesTheFileSystem()
    {
        Dictionary<string, SyntaxTree> sources = ProductionSources().ToDictionary(s => s.Path, s => s.Tree, StringComparer.Ordinal);

        string[] stale = [.. _allowed.Keys.Where(path => !sources.TryGetValue(path, out SyntaxTree? tree) || !FileSystemUses(tree).Any())];

        Assert.True(stale.Length == 0, "Remove from the list: " + string.Join(", ", stale));
    }

    [Fact]
    public void FileStorage_ConfinesItsPaths_WithResolveWithinRoot()
    {
        string[] unconfined =
        [
            .. ProductionSources()
                .Where(s => s.Path.StartsWith(FileStorageProject + "/", StringComparison.Ordinal) && FileSystemUses(s.Tree).Any())
                .Where(s => !CallsResolveWithinRoot(s.Tree))
                .Select(s => s.Path),
        ];

        Assert.True(unconfined.Length == 0, "No StoragePath.ResolveWithinRoot: " + string.Join(", ", unconfined));
    }

    /// <summary>
    /// The reader sees every way the code names a file-system type (a static call, the fully qualified <c>System.IO.File</c>
    /// a controller needs next to <c>ControllerBase.File</c>, a constructor, a target-typed <c>new</c>'s declared type) and
    /// ignores a member that merely shares a name (<c>request.File</c>). Without this, a reader that found nothing would pass.
    /// </summary>
    [Fact]
    public void TheReader_FindsEveryKindOfFileSystemUse_AndOnlyThose()
    {
        SyntaxTree uses = Parse("""
            class Uses
            {
                void A() { File.Exists("a"); }
                void B() { System.IO.File.Exists("b"); }
                void C() { using var s = new FileStream("c", FileMode.Open); }
                void D() { FileStream? s = new("d", FileMode.Open); }
                void E() { Directory.CreateDirectory("e"); }
                void F() { var i = new DirectoryInfo("f"); }
            }
            """);
        SyntaxTree none = Parse("""
            class None
            {
                void A(Request request) { var f = request.File; var n = Path.GetFileName("x"); }
                IActionResult B(Stream s) => File(s, "application/octet-stream");
                void C() { StoragePath.ResolveWithinRoot("root", "x"); }
            }
            """);

        Assert.Equal([3, 4, 5, 6, 7, 8], FileSystemUses(uses));
        Assert.Empty(FileSystemUses(none));
        Assert.False(CallsResolveWithinRoot(uses));
        Assert.True(CallsResolveWithinRoot(none));
    }

    /// <summary>The lines of <paramref name="tree"/> that name a file-system type, in order, each once.</summary>
    private static IEnumerable<int> FileSystemUses(SyntaxTree tree) =>
        tree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>()
            .Where(IsFileSystemType)
            .Select(name => name.GetLocation().GetLineSpan().StartLinePosition.Line + 1)
            .Distinct();

    /// <summary>
    /// True when <paramref name="name"/> names a file-system type: the receiver of a member access (<c>File.Exists</c>), the
    /// type in <c>System.IO.File</c>, or (for the types that are no common member name) any other position — a constructor,
    /// a declared variable type.
    /// </summary>
    private static bool IsFileSystemType(IdentifierNameSyntax name)
    {
        string text = name.Identifier.ValueText;
        if (!_fileSystemTypes.Contains(text)) return false;

        return name.Parent switch
        {
            MemberAccessExpressionSyntax access when access.Expression == name => true,
            MemberAccessExpressionSyntax access when access.Name == name => access.Expression.ToString() == "System.IO",
            QualifiedNameSyntax qualified when qualified.Right == name => qualified.Left.ToString() == "System.IO",
            InvocationExpressionSyntax => false,
            _ => text is not ("File" or "Directory"),
        };
    }

    /// <summary>True when <paramref name="tree"/> calls <c>StoragePath.ResolveWithinRoot</c>.</summary>
    private static bool CallsResolveWithinRoot(SyntaxTree tree) =>
        tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Any(call => call.Expression is MemberAccessExpressionSyntax
        {
            Expression: IdentifierNameSyntax { Identifier.ValueText: "StoragePath" },
            Name.Identifier.ValueText: "ResolveWithinRoot",
        });

    /// <summary>Parses a source text.</summary>
    private static SyntaxTree Parse(string source) =>
        CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));

    /// <summary>
    /// Every <c>.cs</c> file of the production projects (not <c>*.Tests</c>, not the PostgreSQL scaffold), parsed, with its
    /// repository-relative path written with forward slashes.
    /// </summary>
    private static IEnumerable<(string Path, SyntaxTree Tree)> ProductionSources()
    {
        string root = RepositoryPaths.Root;

        return Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Select(Path.GetDirectoryName)
            .OfType<string>()
            .Where(dir => !RepositoryPaths.HasSegment(dir, "bin") && !RepositoryPaths.HasSegment(dir, "obj"))
            .Where(IsProductionProject)
            .SelectMany(dir => Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            .Where(path => !RepositoryPaths.HasSegment(path, "bin") && !RepositoryPaths.HasSegment(path, "obj"))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => (Path.GetRelativePath(root, path).Replace('\\', '/'), Parse(File.ReadAllText(path))));

        static bool IsProductionProject(string dir)
        {
            string name = Path.GetFileName(dir);
            return name.StartsWith("Maroik.", StringComparison.Ordinal)
                   && !name.EndsWith(".Tests", StringComparison.Ordinal)
                   && name != "Maroik.Core.PostgreSQL";
        }
    }
}
