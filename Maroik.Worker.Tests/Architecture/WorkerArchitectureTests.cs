using System.Reflection;
using Maroik.Core.Contract.Interfaces;
using Maroik.Worker.Workers;
using NetArchTest.Rules;

namespace Maroik.Worker.Tests.Architecture;

/// <summary>
/// Architecture test enforcing that Maroik.Worker only consumes the infrastructure
/// implementations (Maroik.Core.Client, Maroik.Core.Repository) via interfaces declared
/// in Maroik.Core.Contract. Direct dependencies on those implementation namespaces are
/// forbidden for all types except Program and WorkerHost (the composition root: Program runs what
/// WorkerHost composes), which wire up implementations into the DI container — mirroring WebsiteArchitectureTests.
/// </summary>
public class WorkerArchitectureTests
{
    private static readonly Assembly _workerAssembly = typeof(EmailConsumerWorker).Assembly;
    private static readonly Assembly _contractAssembly = typeof(IAccountRepository).Assembly;

    private const string RepositoryNamespace = "Maroik.Core.Repository";
    private const string ClientNamespace = "Maroik.Core.Client";
    private const string PostgreSqlNamespace = "Maroik.Core.PostgreSQL";

    /// <summary>
    /// Full names of every repository interface (<c>I*Repository</c>) declared in
    /// Maroik.Core.Contract.Interfaces. Namespace-based checks alone don't stop a Worker type
    /// from injecting one of these directly — they live in Contract alongside service and
    /// client interfaces, so bypassing the service layer this way doesn't touch
    /// Maroik.Core.Repository at all and slips past the namespace-based rules above.
    /// </summary>
    private static readonly string[] _repositoryInterfaceFullNames =
    [
        .. _contractAssembly.GetTypes()
            .Where(t => t is { IsInterface: true, Namespace: "Maroik.Core.Contract.Interfaces" })
            .Where(t => t.Name.EndsWith("Repository", StringComparison.Ordinal))
            .Select(t => t.FullName!)
    ];

    /// <summary>Worker types except composition root should not depend on repository namespace.</summary>
    [Fact]
    public void WorkerTypes_ExceptCompositionRoot_ShouldNot_DependOnRepositoryNamespace()
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(_workerAssembly)
            .That()
            .DoNotHaveName("Program").And().DoNotHaveName("WorkerHost")
            .ShouldNot()
            .HaveDependencyOn(RepositoryNamespace)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "Non-composition-root types in Maroik.Worker must not reference " +
            "Maroik.Core.Repository directly. " +
            "Use repository interfaces from Maroik.Core.Contract instead.\n" +
            "Failing types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// Worker types except composition root should not depend on repository interfaces,
    /// even though those interfaces are declared in Maroik.Core.Contract (not
    /// Maroik.Core.Repository). Only Maroik.Core.Service may consume them.
    /// </summary>
    [Fact]
    public void WorkerTypes_ExceptCompositionRoot_ShouldNot_DependOnRepositoryInterfaces()
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(_workerAssembly)
            .That()
            .DoNotHaveName("Program").And().DoNotHaveName("WorkerHost")
            .ShouldNot()
            .HaveDependencyOnAny(_repositoryInterfaceFullNames)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "Non-composition-root types in Maroik.Worker must not depend on repository " +
            "interfaces (I*Repository) directly. Even though these are declared in " +
            "Maroik.Core.Contract, only Maroik.Core.Service may consume them — inject a " +
            "service interface instead.\n" +
            "Failing types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>Worker types except composition root should not depend on client namespace.</summary>
    [Fact]
    public void WorkerTypes_ExceptCompositionRoot_ShouldNot_DependOnClientNamespace()
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(_workerAssembly)
            .That()
            .DoNotHaveName("Program").And().DoNotHaveName("WorkerHost")
            .ShouldNot()
            .HaveDependencyOn(ClientNamespace)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "Non-composition-root types in Maroik.Worker must not reference " +
            "Maroik.Core.Client directly. " +
            "Use client interfaces from Maroik.Core.Contract instead.\n" +
            "Failing types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// No type in Maroik.Worker, including Program, should depend on Maroik.Core.PostgreSQL.
    /// Even the composition root only needs to register EF Core via
    /// <c>AddRepositoryContext</c> in Maroik.Core.Repository, so there is no legitimate
    /// reason for Worker to reference the PostgreSQL project at all.
    /// </summary>
    [Fact]
    public void WorkerTypes_ShouldNot_DependOnPostgreSqlNamespace()
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(_workerAssembly)
            .That()
            .ResideInNamespaceStartingWith("Maroik.Worker")
            .ShouldNot()
            .HaveDependencyOn(PostgreSqlNamespace)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "Maroik.Worker must not reference Maroik.Core.PostgreSQL directly, not even from " +
            "Program. Register EF Core via AddRepositoryContext in Maroik.Core.Repository instead.\n" +
            "Failing types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }
}
