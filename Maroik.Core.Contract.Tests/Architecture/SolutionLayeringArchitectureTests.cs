using System.Xml.Linq;

namespace Maroik.Core.Contract.Tests.Architecture;

/// <summary>
/// Solution-wide architecture validation. Where the per-layer <c>*ArchitectureTests</c> classes
/// each guard one assembly by reflecting over its compiled types, this class enforces the
/// <b>project dependency graph itself</b> — the <c>&lt;ProjectReference&gt;</c> and
/// <c>&lt;PackageReference&gt;</c> edges declared in every production <c>.csproj</c> — so a
/// forbidden edge fails the build the moment it is added to a project file, before any code is
/// written against it.
///
/// It encodes the ArchNet / Clean-Architecture rules for Maroik:
/// <list type="number">
///   <item>Domain and Contract are completely pure — BCL + the <c>ErrorOr</c> result type only
///         (Contract additionally depends on Domain). No other package, no other project.</item>
///   <item>Client / Repository (infrastructure) never reach another concrete layer: they build on
///         Contract abstractions and must not reference each other or Service, and external-system
///         packages (EF Core/Npgsql, MailKit, RabbitMQ) stay confined to the layer that owns them.</item>
///   <item>Service orchestrates over Contract abstractions only — it must not project-reference any
///         infrastructure/persistence project.</item>
///   <item>Dependencies flow strictly inward: no core project (Domain, Contract, Service, Client,
///         Repository, PostgreSQL) may reference an application host (Website, Worker, FileStorage),
///         and the whole graph is acyclic.</item>
/// </list>
/// Every rule here is an xUnit assertion, so a violation fails <c>dotnet test</c>.
/// </summary>
public class SolutionLayeringArchitectureTests
{
    // ── Layer names (project = assembly name, minus the .csproj) ──────────────────────────────
    /// <summary>Project / assembly name of the Domain layer.</summary>
    private const string Domain = "Maroik.Core.Domain";
    /// <summary>Project / assembly name of the Contract layer.</summary>
    private const string Contract = "Maroik.Core.Contract";
    /// <summary>Project / assembly name of the EF Core persistence model.</summary>
    private const string PostgreSql = "Maroik.Core.PostgreSQL";
    /// <summary>Project / assembly name of the external-system clients.</summary>
    private const string Client = "Maroik.Core.Client";
    /// <summary>Project / assembly name of the Repository layer.</summary>
    private const string Repository = "Maroik.Core.Repository";
    /// <summary>Project / assembly name of the Service layer.</summary>
    private const string Service = "Maroik.Core.Service";
    /// <summary>Project / assembly name of the web host.</summary>
    private const string Website = "Maroik.Website";
    /// <summary>Project / assembly name of the background worker host.</summary>
    private const string Worker = "Maroik.Worker";
    /// <summary>Project / assembly name of the file-storage service host.</summary>
    private const string FileStorage = "Maroik.FileStorage";

    /// <summary>The six inner layers. None of them may depend on an application host project.</summary>
    private static readonly string[] _coreLayers =
        [Domain, Contract, PostgreSql, Client, Repository, Service];

    /// <summary>The three composition-root / host projects that wire everything together.</summary>
    private static readonly string[] _applicationHosts = [Website, Worker, FileStorage];

    /// <summary>
    /// The only <c>&lt;ProjectReference&gt;</c> edges allowed out of each production project.
    /// Anything a project references that is not in its set here is an architecture violation.
    /// </summary>
    private static readonly Dictionary<string, string[]> _allowedProjectReferences = new()
    {
        [Domain] = [],
        [Contract] = [Domain],
        [PostgreSql] = [],
        [Client] = [Contract],
        [Repository] = [Contract, PostgreSql],
        [Service] = [Contract],
        [Website] = [Contract, Client, Repository, Service],
        [Worker] = [Contract, Client, Repository, Service],
        [FileStorage] = [Contract, Client],
    };

    /// <summary>
    /// External-system packages and the only projects allowed to reference them directly. Keeping
    /// EF Core in Repository, the SMTP client in Client, and the message-broker client in Client
    /// (publisher) + Worker (consumer host) is what stops another layer from talking to an external
    /// system without going through a Contract interface.
    /// </summary>
    private static readonly (string PackagePrefix, string[] AllowedProjects, string Purpose)[] _confinedPackages =
    [
        ("Microsoft.EntityFrameworkCore", [Repository, PostgreSql], "EF Core persistence"),
        ("Npgsql", [Repository, PostgreSql], "PostgreSQL driver"),
        ("MailKit", [Client], "SMTP mail transport"),
        ("RabbitMQ.Client", [Client, Worker], "RabbitMQ message broker"),
    ];

    /// <summary>Every non-test project, parsed once on first use.</summary>
    private static readonly Lazy<IReadOnlyList<ProjectFile>> _productionProjects =
        new(LoadProductionProjects);

    // ─────────────────────────────────────────────────────────────────────────────────────────
    // Rule 1 — Domain and Contract are completely pure
    // ─────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Domain is the innermost layer: no project reference at all, and no NuGet package
    /// except <c>ErrorOr</c> (the shared lightweight result/error object).</summary>
    [Fact]
    public void Domain_MustHaveNoProjectReferences_AndOnlyErrorOrPackage()
    {
        ProjectFile domain = GetProject(Domain);

        Assert.True(
            domain.ProjectReferences.Count == 0,
            $"{Domain} must not have any <ProjectReference>. The domain layer is the core of the " +
            "onion and depends on nothing.\nDeclared: " + string.Join(", ", domain.ProjectReferences));

        AssertOnlyErrorOrPackage(domain);
    }

    /// <summary>Contract is a pure abstraction layer: it may reference Domain and nothing else,
    /// and — like Domain — no NuGet package except <c>ErrorOr</c>.</summary>
    [Fact]
    public void Contract_MustReferenceOnlyDomain_AndOnlyErrorOrPackage()
    {
        ProjectFile contract = GetProject(Contract);

        Assert.True(
            contract.ProjectReferences.SetEquals([Domain]),
            $"{Contract} must reference exactly one project: {Domain}. " +
            "Contract holds interfaces + DTOs over the domain and nothing else.\nDeclared: " +
            string.Join(", ", contract.ProjectReferences));

        AssertOnlyErrorOrPackage(contract);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────
    // Rule 2 — infrastructure never bypasses Contract to reach another concrete layer
    // ─────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Client and Repository are sibling infrastructure layers. Each builds only on
    /// Contract (Repository also on the EF Core model project) — never on each other, never on
    /// Service, never on an application host.</summary>
    [Fact]
    public void InfrastructureLayers_MustBuildOnContractOnly_NotOnEachOtherOrService()
    {
        AssertReferencesAreExactly(Client, Contract);
        AssertReferencesAreExactly(Repository, Contract, PostgreSql);
    }

    /// <summary>External-system client packages stay in the one layer that owns that system, so no
    /// other layer can call the external system directly instead of through a Contract interface.</summary>
    [Fact]
    public void ExternalSystemPackages_MustBeConfinedToTheirOwningLayer()
    {
        List<string> violations = [];

        foreach ((string packagePrefix, string[] allowedProjects, string purpose) in _confinedPackages)
        {
            violations.AddRange(from project in _productionProjects.Value let referencesPackage = project.PackageReferences.Any(p => p.StartsWith(packagePrefix, StringComparison.OrdinalIgnoreCase)) where referencesPackage && !allowedProjects.Contains(project.Name) select $"{project.Name} references '{packagePrefix}*' ({purpose}) — only " + $"{string.Join(" / ", allowedProjects)} may. Depend on the matching " + "Maroik.Core.Contract interface instead.");
        }

        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────
    // Rule 3 — Service orchestrates over abstractions only
    // ─────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The service layer is the sole orchestrator of business logic, and it does that job
    /// purely against Contract abstractions: it must not project-reference Client, Repository or the
    /// EF Core model project.</summary>
    [Fact]
    public void ServiceLayer_MustNotProjectReference_AnyInfrastructureOrPersistenceProject()
    {
        AssertReferencesAreExactly(Service, Contract);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────
    // Rule 4 — dependencies flow strictly inward, and the graph is acyclic
    // ─────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>No inner layer may reference an application host project (Website / Worker /
    /// FileStorage). Composition happens at the outside of the onion, never the inside.</summary>
    [Fact]
    public void CoreLayers_MustNotReference_AnyApplicationHostProject()
    {
        List<string> violations =
        [
            .. from layer in _coreLayers
            let offending = GetProject(layer)
                .ProjectReferences.Where(_applicationHosts.Contains)
                .ToArray()
            where offending.Length > 0
            select $"{layer} references application host(s): {string.Join(", ", offending)}"

        ];

        Assert.True(
            violations.Count == 0,
            "Inner layers must not depend on an application host — dependencies flow inward only.\n" +
            string.Join("\n", violations));
    }

    /// <summary>Every production project's <c>&lt;ProjectReference&gt;</c> set is a subset of the
    /// inward edges allowed for its layer. This is the catch-all that keeps the dependency graph
    /// exactly as designed.</summary>
    [Fact]
    public void EveryProductionProject_MustOnlyReference_ItsAllowedInwardProjects()
    {
        List<string> violations = [];

        foreach (ProjectFile project in _productionProjects.Value)
        {
            if (!_allowedProjectReferences.TryGetValue(project.Name, out string[]? allowed))
            {
                violations.Add(
                    $"{project.Name} is a production project with no entry in _allowedProjectReferences — " +
                    "add it to the map (and to Maroik.sln's layer model) deliberately.");
                continue;
            }

            string[] forbidden = project.ProjectReferences.Except(allowed).ToArray();
            if (forbidden.Length > 0)
                violations.Add(
                    $"{project.Name} references {string.Join(", ", forbidden)} — allowed inward edges " +
                    $"are: {(allowed.Length == 0 ? "(none)" : string.Join(", ", allowed))}");
        }

        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    /// <summary>The project-reference graph over all Maroik projects (production + test) must be a
    /// DAG — a cycle would break the "dependencies flow one direction" rule outright.</summary>
    [Fact]
    public void ProjectReferenceGraph_MustBeAcyclic()
    {
        Dictionary<string, string[]> graph = LoadAllProjects()
            .ToDictionary(p => p.Name, p => p.ProjectReferences.ToArray());

        var visiting = new HashSet<string>();
        var done = new HashSet<string>();
        var cycle = new List<string>();

        foreach (string _ in graph.Keys.Where(HasCycle))
        {
            cycle.Reverse();
            Assert.Fail("Project-reference cycle detected: " + string.Join(" -> ", cycle));
        }
        return;

        bool HasCycle(string node)
        {
            if (done.Contains(node)) return false;
            if (!visiting.Add(node))
            {
                cycle.Add(node);
                return true;
            }

            if (graph.TryGetValue(node, out string[]? deps))
            {
                if (deps.Any(HasCycle))
                {
                    cycle.Add(node);
                    return true;
                }
            }

            visiting.Remove(node);
            done.Add(node);
            return false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Fails unless <paramref name="project"/>'s only NuGet reference (if any) is ErrorOr.</summary>
    private static void AssertOnlyErrorOrPackage(ProjectFile project)
    {
        string[] unexpected =
        [
            .. project.PackageReferences
                .Where(p => p != "ErrorOr")
        ];

        Assert.True(
            unexpected.Length == 0,
            $"{project.Name} must not reference any NuGet package except ErrorOr (the shared " +
            "result/error object). Unexpected: " + string.Join(", ", unexpected));
    }

    /// <summary>Fails unless <paramref name="projectName"/> project-references exactly <paramref name="expected"/>.</summary>
    private static void AssertReferencesAreExactly(string projectName, params string[] expected)
    {
        ProjectFile project = GetProject(projectName);

        Assert.True(
            project.ProjectReferences.SetEquals(expected),
            $"{projectName} must project-reference exactly: {string.Join(", ", expected)}. " +
            $"Declared: {(project.ProjectReferences.Count == 0 ? "(none)" : string.Join(", ", project.ProjectReferences))}. " +
            "Reach every other concern through a Maroik.Core.Contract interface instead.");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────
    // Solution registration — every project is in Maroik.sln, under the right solution folder
    // ─────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A project that exists on disk but is missing from <c>Maroik.sln</c> is invisible to the IDE and to
    /// <c>dotnet build Maroik.sln</c> / CI's solution build.</summary>
    [Fact]
    public void EveryProject_MustBeRegisteredInTheSolution()
    {
        SolutionFile solution = SolutionFile.Load(Path.Combine(FindRepositoryRoot(), "Maroik.sln"));

        string[] missing = [.. LoadAllProjects().Select(p => p.Name).Where(name => !solution.ProjectNames.Contains(name)).Order()];

        Assert.True(missing.Length == 0,
            "Add these projects to Maroik.sln (dotnet sln add <csproj> --solution-folder <src|tests>): " + string.Join(", ", missing));
    }

    /// <summary>Test projects (and the test-support project) live in the <c>tests</c> solution folder, the rest in <c>src</c>.</summary>
    [Fact]
    public void EveryProject_MustBeNestedUnderTheMatchingSolutionFolder()
    {
        SolutionFile solution = SolutionFile.Load(Path.Combine(FindRepositoryRoot(), "Maroik.sln"));
        List<string> wrong =
        [
            .. from project in LoadAllProjects() let isTestSide = project.Name.EndsWith(".Tests", StringComparison.Ordinal) let expected = isTestSide ? "tests" : "src" let actual = solution.FolderOf(project.Name) where actual != expected select $"{project.Name}: in '{actual ?? "(solution root)"}', expected '{expected}'"

        ];

        Assert.True(wrong.Count == 0, "Solution folder mismatch:\n" + string.Join("\n", wrong));
    }

    /// <summary>The parts of <c>Maroik.sln</c> the rules above need: project names, and each project's solution folder.</summary>
    private sealed class SolutionFile
    {
        /// <summary>Every <c>Project(...)</c> entry (projects and solution folders) keyed by its GUID.</summary>
        private readonly Dictionary<string, (string Name, string Path)> _byGuid;
        /// <summary>The <c>NestedProjects</c> section: child GUID to parent (solution folder) GUID.</summary>
        private readonly Dictionary<string, string> _parentByGuid;

        /// <summary>Wraps the parsed entries; created by <c>Load</c>.</summary>
        private SolutionFile(Dictionary<string, (string Name, string Path)> byGuid, Dictionary<string, string> parentByGuid)
        {
            _byGuid = byGuid;
            _parentByGuid = parentByGuid;
        }

        /// <summary>Names of every project entry in the solution (solution folders included).</summary>
        public IReadOnlySet<string> ProjectNames => _byGuid.Values.Select(v => v.Name).ToHashSet(StringComparer.Ordinal);

        /// <summary>Parses the <c>.sln</c> at <paramref name="path"/>.</summary>
        public static SolutionFile Load(string path)
        {
            string text = File.ReadAllText(path);

            Dictionary<string, (string Name, string Path)> byGuid = System.Text.RegularExpressions.Regex
                .Matches(text, """
                               Project\("\{[^}]+\}"\) = "(?<name>[^"]+)", "(?<path>[^"]+)", "\{(?<guid>[^}]+)\}"
                               """)
                .ToDictionary(m => m.Groups["guid"].Value.ToUpperInvariant(), m => (m.Groups["name"].Value, m.Groups["path"].Value));

            Dictionary<string, string> parentByGuid = System.Text.RegularExpressions.Regex
                .Matches(text, @"\{(?<child>[0-9A-Fa-f-]+)\}\s*=\s*\{(?<parent>[0-9A-Fa-f-]+)\}")
                .ToDictionary(m => m.Groups["child"].Value.ToUpperInvariant(), m => m.Groups["parent"].Value.ToUpperInvariant());

            return new SolutionFile(byGuid, parentByGuid);
        }

        /// <summary>The name of the solution folder the project is directly nested in, or <see langword="null"/> at the solution root.</summary>
        public string? FolderOf(string projectName)
        {
            string? guid = _byGuid.FirstOrDefault(kv => kv.Value.Name == projectName).Key;
            return guid != null && _parentByGuid.TryGetValue(guid, out string? parent) && _byGuid.TryGetValue(parent, out var folder)
                ? folder.Name
                : null;
        }
    }

    /// <summary>The production project named <paramref name="name"/>; throws when it does not exist.</summary>
    private static ProjectFile GetProject(string name) =>
        _productionProjects.Value.SingleOrDefault(p => p.Name == name)
        ?? throw new InvalidOperationException(
            $"Production project '{name}' not found under the repository root. " +
            "Known: " + string.Join(", ", _productionProjects.Value.Select(p => p.Name)));

    /// <summary>Every project under the repository root except the <c>*.Tests</c> ones.</summary>
    private static IReadOnlyList<ProjectFile> LoadProductionProjects() =>
    [
        .. LoadAllProjects()
            .Where(p => !p.Name.EndsWith(".Tests", StringComparison.Ordinal))
            .Where(p => p.Name != "Maroik.E2E.Tests")
    ];

    /// <summary>Every <c>Maroik.*.csproj</c> under the repository root (outside <c>bin</c>/<c>obj</c>), parsed and sorted by name.</summary>
    private static IReadOnlyList<ProjectFile> LoadAllProjects()
    {
        string root = FindRepositoryRoot();

        return
        [
            .. Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories)
                .Where(path => !PathHasSegment(path, "bin") && !PathHasSegment(path, "obj"))
                .Where(path => Path.GetFileName(path).StartsWith("Maroik.", StringComparison.Ordinal))
                .Select(ProjectFile.Load)
                .OrderBy(p => p.Name, StringComparer.Ordinal)
        ];
    }

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

    /// <summary>A parsed <c>.csproj</c>: its assembly name and its declared reference edges.</summary>
    private sealed record ProjectFile(
        string Name,
        HashSet<string> ProjectReferences,
        HashSet<string> PackageReferences)
    {
        /// <summary>Parses the <c>.csproj</c> at <paramref name="path"/>.</summary>
        public static ProjectFile Load(string path)
        {
            XDocument doc = XDocument.Load(path);

            HashSet<string> projectRefs = doc.Descendants()
                .Where(e => e.Name.LocalName == "ProjectReference")
                .Select(e => e.Attribute("Include")?.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => Path.GetFileNameWithoutExtension(v!.Replace('\\', '/')))
                .ToHashSet(StringComparer.Ordinal);

            HashSet<string> packageRefs = doc.Descendants()
                .Where(e => e.Name.LocalName is "PackageReference" or "PackageVersion")
                .Select(e => e.Attribute("Include")?.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!.Trim())
                .ToHashSet(StringComparer.Ordinal);

            return new ProjectFile(Path.GetFileNameWithoutExtension(path), projectRefs, packageRefs);
        }
    }
}
