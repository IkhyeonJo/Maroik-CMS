using System.Reflection;
using Maroik.Core.PostgreSQL.Data;
using NetArchTest.Rules;

namespace Maroik.Core.PostgreSQL.Tests.Architecture;

/// <summary>
/// Maroik.Core.PostgreSQL is the persistence model only — the EF Core <c>DbContext</c> and the entity classes that mirror the tables. It sits at
/// the bottom of the dependency graph: it must not know any other Maroik project (domain rules, contracts, services, clients, hosts).
/// </summary>
public class PostgreSqlArchitectureTests
{
    private static readonly Assembly _assembly = typeof(ApplicationDbContext).Assembly;

    private static readonly string[] _otherProjects =
    [
        "Maroik.Core.Domain", "Maroik.Core.Contract", "Maroik.Core.Service", "Maroik.Core.Repository", "Maroik.Core.Client",
        "Maroik.Website", "Maroik.Worker", "Maroik.FileStorage",
    ];

    /// <summary>No type here depends on any other Maroik project.</summary>
    [Fact]
    public void PostgreSql_DependsOnNoOtherMaroikProject()
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(_assembly).ShouldNot().HaveDependencyOnAny(_otherProjects).GetResult();

        Assert.True(result.IsSuccessful, "offending types: " + string.Join(", ", result.FailingTypeNames ?? []));
        Assert.DoesNotContain(_assembly.GetReferencedAssemblies(), a => _otherProjects.Contains(a.Name));
    }

    /// <summary>The entity classes live in <c>Models</c>, the context in <c>Data</c>, and nothing else is defined at the top of the assembly.</summary>
    [Fact]
    public void Types_LiveInModelsOrData()
    {
        var stray = _assembly.GetTypes()
            .Where(t => t is { IsPublic: true, Namespace: not ("Maroik.Core.PostgreSQL.Models" or "Maroik.Core.PostgreSQL.Data") })
            .Select(t => t.FullName).ToList();

        Assert.True(stray.Count == 0, "public types outside Models / Data: " + string.Join(", ", stray));
    }

    /// <summary>The entities are plain data classes: they hold no behavior of their own (business rules belong to the Domain).</summary>
    [Fact]
    public void Entities_HaveNoMethodsOfTheirOwn()
    {
        var withMethods = _assembly.GetTypes()
            .Where(t => t is { Namespace: "Maroik.Core.PostgreSQL.Models", IsPublic: true })
            .Where(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Any(m => !m.IsSpecialName))
            .Select(t => t.Name).ToList();

        Assert.True(withMethods.Count == 0, "entities with methods: " + string.Join(", ", withMethods));
    }
}
