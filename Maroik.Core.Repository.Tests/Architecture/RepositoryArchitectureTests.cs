using System.Reflection;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Primitives;
using Maroik.Core.Repository.Repositories;
using NetArchTest.Rules;

namespace Maroik.Core.Repository.Tests.Architecture;

/// <summary>
/// Architecture test enforcing that Maroik.Core.Repository stays a thin persistence layer:
/// it maps rows to/from domain entities behind Maroik.Core.Contract interfaces and nothing more.
/// It is a sibling of Maroik.Core.Client and Maroik.Core.Service (must not reference either), it
/// must not be reached by an application host, it must not touch an external system that belongs
/// to Client, and it must not run business logic (domain policies) or call the service layer.
/// </summary>
public class RepositoryArchitectureTests
{
    /// <summary>The assembly containing <c>AccountRepository</c>, referenced by these architecture rules.</summary>
    private static readonly Assembly _repositoryAssembly = typeof(AccountRepository).Assembly;
    /// <summary>The assembly containing <c>Entity&lt;TId&gt;</c> (Maroik.Core.Domain), referenced by these architecture rules.</summary>
    private static readonly Assembly _domainAssembly = typeof(Entity<>).Assembly;
    /// <summary>The assembly containing <c>IAccountRepository</c>, referenced by these architecture rules.</summary>
    private static readonly Assembly _contractAssembly = typeof(IAccountRepository).Assembly;

    /// <summary>
    /// Full names of every domain <c>*Policy</c> type (business rules / allowed-value taxonomies).
    /// The persistence layer must not apply one — that is service / domain-entity work.
    /// </summary>
    private static readonly string[] _domainPolicyFullNames =
    [
        .. _domainAssembly.GetTypes()
            .Where(t => t.IsClass && t.Name.EndsWith("Policy", StringComparison.Ordinal))
            .Select(t => t.FullName!)
    ];

    /// <summary>
    /// Full names of every <c>I*Service</c> interface declared in Maroik.Core.Contract.Interfaces.
    /// A repository must never call a service — dependencies flow Service → Repository, never back.
    /// </summary>
    private static readonly string[] _serviceInterfaceFullNames =
    [
        .. _contractAssembly.GetTypes()
            .Where(t => t is { IsInterface: true, Namespace: "Maroik.Core.Contract.Interfaces" })
            .Where(t => t.Name.EndsWith("Service", StringComparison.Ordinal))
            .Select(t => t.FullName!)
    ];

    /// <summary>Repository should not depend on client namespace.</summary>
    [Fact]
    public void Repository_ShouldNot_DependOnClientNamespace()
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(_repositoryAssembly)
            .ShouldNot()
            .HaveDependencyOn("Maroik.Core.Client")
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "Maroik.Core.Repository must not reference Maroik.Core.Client directly. " +
            "These are sibling infrastructure layers and must not depend on each other.\n" +
            "Failing types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>Repository should not depend on service namespace.</summary>
    [Fact]
    public void Repository_ShouldNot_DependOnServiceNamespace()
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(_repositoryAssembly)
            .ShouldNot()
            .HaveDependencyOn("Maroik.Core.Service")
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "Maroik.Core.Repository must not reference Maroik.Core.Service directly. " +
            "Repository is an infrastructure layer and must not depend on the service layer.\n" +
            "Failing types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>Repository must not be reached by, or reach into, an application host project.</summary>
    [Theory]
    [InlineData("Maroik.Website")]
    [InlineData("Maroik.Worker")]
    [InlineData("Maroik.FileStorage")]
    public void Repository_ShouldNot_DependOnApplicationHostLayer(string forbiddenNamespace)
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(_repositoryAssembly)
            .ShouldNot()
            .HaveDependencyOn(forbiddenNamespace)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"Maroik.Core.Repository must not reference {forbiddenNamespace}. Dependencies flow " +
            "inward: a host wires the repository in, never the other way around.\n" +
            "Failing types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// Repository owns exactly one external system — the database (EF Core / Npgsql). Mail and the
    /// message broker are Maroik.Core.Client's job; the repository must not reference their client
    /// libraries.
    /// </summary>
    [Theory]
    [InlineData("MailKit")]
    [InlineData("MimeKit")]
    [InlineData("RabbitMQ.Client")]
    public void Repository_ShouldNot_DependOnAnotherLayersExternalSystemLibrary(string forbiddenNamespace)
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(_repositoryAssembly)
            .ShouldNot()
            .HaveDependencyOn(forbiddenNamespace)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"Maroik.Core.Repository must not reference {forbiddenNamespace}. Sending mail and " +
            "publishing messages belong to Maroik.Core.Client, behind a Maroik.Core.Contract " +
            "interface.\nFailing types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// The persistence layer contains no business logic: it must not depend on any domain
    /// <c>*Policy</c> (business rule / allowed-value taxonomy). Enforcing a policy is domain-entity
    /// or service work; a repository only reads and writes rows.
    /// </summary>
    [Fact]
    public void Repository_ShouldNot_DependOn_DomainPolicies()
    {
        Assert.NotEmpty(_domainPolicyFullNames);

        NetArchTest.Rules.TestResult result = Types.InAssembly(_repositoryAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(_domainPolicyFullNames)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "Maroik.Core.Repository must not depend on a domain policy — the persistence layer holds " +
            "no business rules. Apply the policy in the aggregate/domain method or the service that " +
            "calls the repository.\nPolicies: " + string.Join(", ", _domainPolicyFullNames) +
            "\nFailing types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// A repository must never call a service (<c>I*Service</c> in Maroik.Core.Contract.Interfaces).
    /// The dependency runs Service → Repository only.
    /// </summary>
    [Fact]
    public void Repository_ShouldNot_DependOn_ServiceInterfaces()
    {
        Assert.NotEmpty(_serviceInterfaceFullNames);

        NetArchTest.Rules.TestResult result = Types.InAssembly(_repositoryAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(_serviceInterfaceFullNames)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "Maroik.Core.Repository must not depend on a service interface — dependencies flow " +
            "Service → Repository, never the reverse.\n" +
            "Failing types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }
}
