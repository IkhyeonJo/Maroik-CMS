using Maroik.Core.Contract.Tests.Fixtures;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Maroik.Core.Contract.Tests.ArchitectureRules;

/// <summary>
/// Path traversal has no static analyzer since SecurityCodeScan (SCS0018) left the build, and CA3003 cannot be told about the
/// project's own validator. This class keeps the file system in view instead, at two strengths:
/// <list type="bullet">
///   <item><b>Maroik.FileStorage</b>, the one service whose paths come from a request, is held by the type system: only
///         <c>Helpers/StorageFiles.cs</c> names <c>File</c> / <c>Directory</c> / <c>FileStream</c> / <c>FileInfo</c> /
///         <c>DirectoryInfo</c>, and every member of <c>StorageFiles</c> takes a <c>StoragePath</c> (which only
///         <c>StoragePath.ResolveWithinRoot</c> can make) and no <c>string</c>. An unchecked path cannot reach a file.</item>
///   <item><b>Everywhere else</b> the paths come from server configuration or a fixed folder, and each use is listed in
///         <see cref="_allowed"/> by file, member and API, with the reason its path is safe. The list must match the code
///         exactly: a new use (a new member, or a new API in a listed member) fails until it is listed after checking where
///         its path comes from, and a use that went away fails until its entry goes too.</item>
/// </list>
/// </summary>
public class FileSystemAccessArchitectureTests
{
    /// <summary>The file-system types the rules watch.</summary>
    private static readonly HashSet<string> _fileSystemTypes = ["File", "Directory", "FileStream", "FileInfo", "DirectoryInfo"];

    /// <summary>The project whose file-system access goes only through <see cref="StorageFilesPath"/>.</summary>
    private const string FileStorageProject = "Maroik.FileStorage/";

    /// <summary>The file holding <c>StorageFiles</c>, the only file of <see cref="FileStorageProject"/> that touches the file system.</summary>
    private const string StorageFilesPath = "Maroik.FileStorage/Helpers/StorageFiles.cs";

    /// <summary>
    /// Every file-system use in production code, as "path::member", the APIs it uses there, and why the path is safe. An API is
    /// <c>Type.Member</c> for a call (<c>File.Exists</c>), <c>new Type</c> for a constructor, and the bare type name where the
    /// code only names the type (a declared return type of a target-typed <c>new</c>).
    /// </summary>
    private static readonly Dictionary<string, (string[] Apis, string Reason)> _allowed = new(StringComparer.Ordinal)
    {
        ["Maroik.FileStorage/Helpers/StorageFiles.cs::Exists"] = (["File.Exists"], "takes a StoragePath"),
        ["Maroik.FileStorage/Helpers/StorageFiles.cs::OpenRead"] = (["FileStream"], "takes a StoragePath"),
        ["Maroik.FileStorage/Helpers/StorageFiles.cs::CreateDirectory"] = (["Directory.CreateDirectory"], "takes a StoragePath"),
        ["Maroik.FileStorage/Helpers/StorageFiles.cs::CreateNew"] = (["FileStream"], "takes a StoragePath"),
        ["Maroik.FileStorage/Helpers/StorageFiles.cs::MakeReadable"] = (["File.SetUnixFileMode"], "takes a StoragePath"),
        ["Maroik.Website/Middlewares/AvatarCacheMiddleware.cs::InvokeAsync"] = (
            ["Directory.CreateDirectory", "File.Exists", "File.Move", "File.WriteAllBytesAsync"],
            "a fixed wwwroot folder, under a file name UploadedAvatarFileName accepted (a GUID with an allowed image extension)"),
        ["Maroik.Core.Service/Services/DashboardService.cs::GetServerResourceSummary"] = (
            ["File.Exists", "File.ReadAllText"],
            "the two server-resource report files, whose paths are the controller's constants, never request input"),
        ["Maroik.Website/Program.cs::<top-level>"] = (
            ["Directory.CreateDirectory", "new DirectoryInfo"],
            "the Data Protection key-ring folder named by server configuration"),
    };

    [Fact]
    public void TheFileSystem_IsTouchedOnlyByTheListedUses()
    {
        Dictionary<string, string[]> actual = ProductionSources()
            .SelectMany(s => FileSystemUses(s.Tree).Select(use => (Key: $"{s.Path}::{use.Member}", use.Api)))
            .GroupBy(use => use.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(use => use.Api).Distinct().Order(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);

        string[] differences =
        [
            .. actual.Where(a => !_allowed.TryGetValue(a.Key, out var allowed) || !allowed.Apis.Order(StringComparer.Ordinal).SequenceEqual(a.Value))
                .Select(a => $"{a.Key} uses [{string.Join(", ", a.Value)}]" +
                             (_allowed.TryGetValue(a.Key, out var allowed) ? $", listed as [{string.Join(", ", allowed.Apis)}]" : ", not listed")),
            .. _allowed.Keys.Where(key => !actual.ContainsKey(key)).Select(key => $"{key} is listed but no longer touches the file system"),
        ];

        Assert.True(differences.Length == 0, string.Join(Environment.NewLine, differences));
    }

    [Fact]
    public void InFileStorage_OnlyStorageFiles_TouchesTheFileSystem()
    {
        string[] others =
        [
            .. ProductionSources()
                .Where(s => s.Path.StartsWith(FileStorageProject, StringComparison.Ordinal) && s.Path != StorageFilesPath)
                .Where(s => FileSystemUses(s.Tree).Any())
                .Select(s => s.Path),
        ];

        Assert.True(others.Length == 0, "Use StorageFiles (with a StoragePath) instead: " + string.Join(", ", others));
    }

    [Fact]
    public void StorageFiles_TakesOnlyStoragePaths()
    {
        SyntaxTree storageFiles = ProductionSources().Single(s => s.Path == StorageFilesPath).Tree;

        Assert.Equal([], UnconfinedMembers(storageFiles));
        Assert.NotEmpty(storageFiles.GetRoot(TestContext.Current.CancellationToken).DescendantNodes().OfType<MethodDeclarationSyntax>());
    }

    /// <summary>
    /// The reader sees every way the code names a file-system type (a static call, the fully qualified <c>System.IO.File</c>
    /// a controller needs next to <c>ControllerBase.File</c>, a constructor, a declared type) under the member it sits in, and
    /// ignores a member that merely shares a name (<c>request.File</c>, <c>ControllerBase.File(...)</c>). Without this, a
    /// reader that found nothing would pass every rule.
    /// </summary>
    [Fact]
    public void TheReader_FindsEveryKindOfFileSystemUse_AndOnlyThose()
    {
        SyntaxTree uses = Parse("""
            Directory.CreateDirectory("top");
            class Uses
            {
                void A() { File.Exists("a"); System.IO.File.Exists("a"); }
                void B() { using var s = new FileStream("b", FileMode.Open); }
                FileStream C() => new("c", FileMode.Open);
                void D() { var i = new DirectoryInfo("d"); }
                void E() { Action a = () => File.Delete("e"); }
            }
            """);
        SyntaxTree none = Parse("""
            class None
            {
                void A(Request request) { var f = request.File; var n = Path.GetFileName("x"); }
                IActionResult B(Stream s) => File(s, "application/octet-stream");
            }
            """);

        Assert.Equal(
            ["<top-level>:Directory.CreateDirectory", "A:File.Exists", "A:File.Exists", "B:new FileStream", "C:FileStream",
             "D:new DirectoryInfo", "E:File.Delete"],
            FileSystemUses(uses).Select(u => $"{u.Member}:{u.Api}"));
        Assert.Empty(FileSystemUses(none));
    }

    /// <summary>The members of a <c>StorageFiles</c> sample that take a <c>string</c> or no <c>StoragePath</c> are reported.</summary>
    [Fact]
    public void TheStorageFilesRule_FlagsAMemberThatTakesAString()
    {
        SyntaxTree sample = Parse("""
            static class StorageFiles
            {
                internal static bool Exists(StoragePath file) => true;
                internal static bool Raw(string path) => true;
                internal static bool Both(StoragePath file, string extra) => true;
                internal static void Nothing() { }
            }
            """);

        Assert.Equal(["Raw", "Both", "Nothing"], UnconfinedMembers(sample));
    }

    /// <summary>Methods of <paramref name="tree"/> that take a <c>string</c>, or take no <c>StoragePath</c>.</summary>
    private static string[] UnconfinedMembers(SyntaxTree tree) =>
    [
        .. tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(m => m.ParameterList.Parameters.Any(p => p.Type?.ToString() is "string" or "string?")
                        || !m.ParameterList.Parameters.Any(p => p.Type?.ToString() == "StoragePath"))
            .Select(m => m.Identifier.ValueText),
    ];

    /// <summary>Every place <paramref name="tree"/> names a file-system type: the member it sits in, and the API it names.</summary>
    private static IEnumerable<(string Member, string Api)> FileSystemUses(SyntaxTree tree) =>
        tree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>()
            .Select(name => (Name: name, Api: ApiOf(name)))
            .Where(use => use.Api != null)
            .Select(use => (MemberOf(use.Name), use.Api!));

    /// <summary>
    /// The API <paramref name="name"/> names when it is a file-system type, otherwise null: <c>File.Exists</c> for the receiver
    /// of a call (also through <c>System.IO.File</c>), <c>new FileStream</c> for a constructor, and the bare type name anywhere
    /// else — but only for the types that are no common member name, so <c>request.File</c> and <c>File(...)</c> are not uses.
    /// </summary>
    private static string? ApiOf(IdentifierNameSyntax name)
    {
        string type = name.Identifier.ValueText;
        if (!_fileSystemTypes.Contains(type)) return null;

        SyntaxNode receiver = name.Parent is MemberAccessExpressionSyntax qualified && qualified.Name == name
                              && qualified.Expression.ToString() == "System.IO"
            ? qualified
            : name;
        if (receiver.Parent is MemberAccessExpressionSyntax access && access.Expression == receiver)
            return $"{type}.{access.Name.Identifier.ValueText}";
        if (receiver != name || type is "File" or "Directory")
            return null;
        if (name.Parent is ObjectCreationExpressionSyntax)
            return $"new {type}";
        return name.Parent is MemberAccessExpressionSyntax or InvocationExpressionSyntax ? null : type;
    }

    /// <summary>The name of the method, local function, constructor or property <paramref name="node"/> sits in; top-level statements are "&lt;top-level&gt;".</summary>
    private static string MemberOf(SyntaxNode node) =>
        node.Ancestors().Select(a => a switch
        {
            LocalFunctionStatementSyntax f => f.Identifier.ValueText,
            MethodDeclarationSyntax m => m.Identifier.ValueText,
            ConstructorDeclarationSyntax c => c.Identifier.ValueText,
            PropertyDeclarationSyntax p => p.Identifier.ValueText,
            _ => null,
        }).FirstOrDefault(member => member != null) ?? "<top-level>";

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
