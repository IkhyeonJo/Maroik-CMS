using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Maroik.Core.Contract.Tests.ArchitectureRules;

/// <summary>
/// Solution-wide folder roles: an interface a project defines lives in <c>Interfaces/</c>, a static class of extension
/// members lives in <c>Extensions/</c> (or in <c>Mappers/</c> when it is a <c>*Mapper</c>), and those two folders hold
/// nothing else. The build already makes every folder a plural noun (MAROIK0001), every namespace match its folder
/// (IDE0130) and every file hold one type named after it (SA1402 / SA1649); this class covers what a folder is <em>for</em>,
/// which no analyzer sees.
/// <para>
/// It reads the production sources with the Roslyn parser rather than reflecting over the assemblies, so one test covers
/// every production project (the hosts included) without this project referencing them — which
/// <see cref="SolutionLayeringArchitectureTests"/> forbids. <c>Maroik.Core.PostgreSQL</c> is EF Core scaffold output and
/// is left out, and so are the <c>*.Tests</c> projects, whose <c>Extensions/</c> folders hold the tests of extensions.
/// </para>
/// </summary>
public class FolderRoleArchitectureTests
{
    /// <summary>The folder every interface lives in.</summary>
    private const string InterfacesFolder = "Interfaces";

    /// <summary>The folder every static class of extension members lives in.</summary>
    private const string ExtensionsFolder = "Extensions";

    /// <summary>The folder of the <c>*Mapper</c> classes, which are extension classes too.</summary>
    private const string MappersFolder = "Mappers";

    /// <summary>Every top-level type of the production sources, parsed once on first use.</summary>
    private static readonly Lazy<IReadOnlyList<SourceType>> _productionTypes = new(LoadProductionTypes);

    /// <summary>An interface outside <c>Interfaces/</c> is one a reader cannot find where every other one is.</summary>
    [Fact]
    public void EveryInterface_LivesInAnInterfacesFolder()
    {
        string[] violations =
        [
            .. _productionTypes.Value
                .Where(t => t.IsInterface && t.Folder != InterfacesFolder)
                .Select(t => $"{t.Path}: interface {t.Name} is not in an {InterfacesFolder}/ folder"),
        ];

        Assert.True(violations.Length == 0, string.Join("\n", violations));
    }

    /// <summary><c>Interfaces/</c> holds interfaces only; a record or class a contract carries belongs in <c>Dtos/</c>.</summary>
    [Fact]
    public void AnInterfacesFolder_HoldsOnlyInterfaces()
    {
        string[] violations =
        [
            .. _productionTypes.Value
                .Where(t => t.Folder == InterfacesFolder && !t.IsInterface)
                .Select(t => $"{t.Path}: {t.Name} is not an interface but sits in {InterfacesFolder}/"),
        ];

        Assert.True(violations.Length == 0, string.Join("\n", violations));
    }

    /// <summary>A class of extension members lives in <c>Extensions/</c>, or in <c>Mappers/</c> when it is a <c>*Mapper</c>.</summary>
    [Fact]
    public void EveryExtensionClass_LivesInAnExtensionsFolder_OrIsAMapper()
    {
        string[] violations =
        [
            .. _productionTypes.Value
                .Where(t => t.HasExtensionMembers)
                .Where(t => t.Folder != ExtensionsFolder && !(t.Folder == MappersFolder && t.Name.EndsWith("Mapper", StringComparison.Ordinal)))
                .Select(t => $"{t.Path}: {t.Name} declares extension members but is not in an {ExtensionsFolder}/ folder " +
                             $"(nor a *Mapper in {MappersFolder}/)"),
        ];

        Assert.True(violations.Length == 0, string.Join("\n", violations));
    }

    /// <summary><c>Extensions/</c> holds extension classes only; a plain static helper is named for what it does elsewhere.</summary>
    [Fact]
    public void AnExtensionsFolder_HoldsOnlyExtensionClasses()
    {
        string[] violations =
        [
            .. _productionTypes.Value
                .Where(t => t.Folder == ExtensionsFolder && !t.HasExtensionMembers)
                .Select(t => $"{t.Path}: {t.Name} declares no extension member but sits in {ExtensionsFolder}/"),
        ];

        Assert.True(violations.Length == 0, string.Join("\n", violations));
    }

    /// <summary>
    /// The parser sees what the rules above judge: an interface, an extension method (<c>this</c> parameter) and a C# 14
    /// <c>extension</c> block. Without this a parse that silently found nothing would pass every rule.
    /// </summary>
    [Fact]
    public void TheSourceReader_RecognisesInterfacesAndBothKindsOfExtensionMembers()
    {
        SourceType[] parsed =
        [
            .. ReadTypes("Sample/Interfaces/IThing.cs", "namespace N;\npublic interface IThing;\n"),
            .. ReadTypes("Sample/Extensions/ThisExtensions.cs",
                "namespace N;\npublic static class ThisExtensions { public static int Twice(this int value) => value * 2; }\n"),
            .. ReadTypes("Sample/Extensions/BlockExtensions.cs",
                "namespace N;\npublic static class BlockExtensions { extension(int value) { public int Twice() => value * 2; } }\n"),
            .. ReadTypes("Sample/Helpers/Plain.cs", "namespace N;\npublic static class Plain { public static int Twice(int value) => value * 2; }\n"),
        ];

        Assert.Equal(
            ["IThing:Interfaces:interface", "ThisExtensions:Extensions:extensions", "BlockExtensions:Extensions:extensions", "Plain:Helpers:"],
            parsed.Select(t => $"{t.Name}:{t.Folder}:{(t.IsInterface ? "interface" : t.HasExtensionMembers ? "extensions" : "")}"));
        Assert.Contains(_productionTypes.Value, t => t.Name == "IUnitOfWork" && t.IsInterface);
    }

    /// <summary>Every top-level type declared in the production projects' <c>.cs</c> files.</summary>
    private static IReadOnlyList<SourceType> LoadProductionTypes()
    {
        string root = FindRepositoryRoot();

        return
        [
            .. Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories)
                .Select(Path.GetDirectoryName)
                .OfType<string>()
                .Where(dir => !PathHasSegment(dir, "bin") && !PathHasSegment(dir, "obj"))
                .Where(IsProductionProject)
                .SelectMany(dir => Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                .Where(path => !PathHasSegment(path, "bin") && !PathHasSegment(path, "obj"))
                .OrderBy(path => path, StringComparer.Ordinal)
                .SelectMany(path => ReadTypes(Path.GetRelativePath(root, path), File.ReadAllText(path))),
        ];

        static bool IsProductionProject(string dir)
        {
            string name = Path.GetFileName(dir);
            return name.StartsWith("Maroik.", StringComparison.Ordinal)
                   && !name.EndsWith(".Tests", StringComparison.Ordinal)
                   && name != "Maroik.Core.PostgreSQL";
        }
    }

    /// <summary>Parses one source file and returns its top-level types.</summary>
    private static IEnumerable<SourceType> ReadTypes(string relativePath, string source)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview), relativePath);
        string folder = Path.GetFileName(Path.GetDirectoryName(relativePath)) ?? "";

        return tree.GetCompilationUnitRoot()
            .DescendantNodes(node => node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax)
            .OfType<BaseTypeDeclarationSyntax>()
            .Select(type => new SourceType(
                relativePath,
                folder,
                type.Identifier.ValueText,
                type is InterfaceDeclarationSyntax,
                HasExtensionMembers(type)));
    }

    /// <summary>True when <paramref name="type"/> declares an extension method or a C# 14 <c>extension</c> block.</summary>
    private static bool HasExtensionMembers(BaseTypeDeclarationSyntax type) =>
        type is TypeDeclarationSyntax declaration
        && declaration.Members.Any(member =>
            member is ExtensionBlockDeclarationSyntax
            || member is MethodDeclarationSyntax { ParameterList.Parameters: [var first, ..] }
               && first.Modifiers.Any(SyntaxKind.ThisKeyword));

    /// <summary>True when <paramref name="path"/> has a directory segment equal to <paramref name="segment"/> (case-insensitive).</summary>
    private static bool PathHasSegment(string path, string segment) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Contains(segment, StringComparer.OrdinalIgnoreCase);

    /// <summary>Walks up from the test binary's folder to the directory holding <c>Maroik.sln</c>.</summary>
    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Maroik.sln")))
            dir = dir.Parent;
        if (dir == null)
            throw new InvalidOperationException(
                "Could not locate the repository root (Maroik.sln) from " + AppContext.BaseDirectory);
        return dir.FullName;
    }

    /// <summary>A top-level type as the folder-role rules see it.</summary>
    private sealed record SourceType(string Path, string Folder, string Name, bool IsInterface, bool HasExtensionMembers);
}
