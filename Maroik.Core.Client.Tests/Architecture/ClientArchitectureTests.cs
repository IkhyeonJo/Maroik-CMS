using System.Reflection;
using Maroik.Core.Client.Clients;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Primitives;
using NetArchTest.Rules;

namespace Maroik.Core.Client.Tests.Architecture;

/// <summary>
/// Architecture test enforcing that Maroik.Core.Client stays a thin adapter to external systems
/// (SMTP mail, file storage, ClamAV, RabbitMQ) behind Maroik.Core.Contract interfaces. It is a
/// sibling of Maroik.Core.Repository and Maroik.Core.Service (must not reference either), it must
/// not be reached by an application host, it must not do persistence (EF Core is Repository's
/// job), and it must not run business logic (domain policies) or call the service layer.
/// </summary>
public class ClientArchitectureTests
{
    /// <summary>The assembly containing <c>FileClient</c>, referenced by these architecture rules.</summary>
    private static readonly Assembly _clientAssembly = typeof(FileClient).Assembly;
    /// <summary>The assembly containing <c>Entity&lt;TId&gt;</c> (Maroik.Core.Domain), referenced by these architecture rules.</summary>
    private static readonly Assembly _domainAssembly = typeof(Entity<>).Assembly;
    /// <summary>The assembly containing <c>IAccountRepository</c>, referenced by these architecture rules.</summary>
    private static readonly Assembly _contractAssembly = typeof(IAccountRepository).Assembly;

    /// <summary>
    /// Full names of every domain <c>*Policy</c> type (business rules / allowed-value taxonomies).
    /// An external-system adapter must not apply one.
    /// </summary>
    private static readonly string[] _domainPolicyFullNames =
    [
        .. _domainAssembly.GetTypes()
            .Where(t => t.IsClass && t.Name.EndsWith("Policy", StringComparison.Ordinal))
            .Select(t => t.FullName!)
    ];

    /// <summary>
    /// Full names of every <c>I*Service</c> interface declared in Maroik.Core.Contract.Interfaces.
    /// A client must never call a service — dependencies flow Service → Client, never back.
    /// </summary>
    private static readonly string[] _serviceInterfaceFullNames =
    [
        .. _contractAssembly.GetTypes()
            .Where(t => t is { IsInterface: true, Namespace: "Maroik.Core.Contract.Interfaces" })
            .Where(t => t.Name.EndsWith("Service", StringComparison.Ordinal))
            .Select(t => t.FullName!)
    ];

    /// <summary>Client should not depend on repository namespace.</summary>
    [Fact]
    public void Client_ShouldNot_DependOnRepositoryNamespace()
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(_clientAssembly)
            .ShouldNot()
            .HaveDependencyOn("Maroik.Core.Repository")
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "Maroik.Core.Client must not reference Maroik.Core.Repository directly. " +
            "These are sibling infrastructure layers and must not depend on each other.\n" +
            "Failing types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>Client should not depend on service namespace.</summary>
    [Fact]
    public void Client_ShouldNot_DependOnServiceNamespace()
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(_clientAssembly)
            .ShouldNot()
            .HaveDependencyOn("Maroik.Core.Service")
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "Maroik.Core.Client must not reference Maroik.Core.Service directly. " +
            "Client is an infrastructure layer and must not depend on the service layer.\n" +
            "Failing types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>Client must not reach into an application host, nor into the persistence layer.</summary>
    [Theory]
    [InlineData("Maroik.Core.PostgreSQL")]
    [InlineData("Maroik.Website")]
    [InlineData("Maroik.Worker")]
    [InlineData("Maroik.FileStorage")]
    public void Client_ShouldNot_DependOnPersistenceOrApplicationHostLayer(string forbiddenNamespace)
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(_clientAssembly)
            .ShouldNot()
            .HaveDependencyOn(forbiddenNamespace)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"Maroik.Core.Client must not reference {forbiddenNamespace}. Client adapts external " +
            "systems; it does not read the database and it is wired in by a host, not the reverse.\n" +
            "Failing types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// Persistence is Maroik.Core.Repository's job. The client layer must not reference EF Core or
    /// the Postgres driver — if a client needs stored data it takes it as a method argument or via
    /// a Maroik.Core.Contract interface.
    /// </summary>
    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Npgsql")]
    public void Client_ShouldNot_DependOn_PersistenceLibrary(string forbiddenNamespace)
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(_clientAssembly)
            .ShouldNot()
            .HaveDependencyOn(forbiddenNamespace)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"Maroik.Core.Client must not reference {forbiddenNamespace}. Database access belongs " +
            "to Maroik.Core.Repository.\nFailing types: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// The client layer contains no business logic: it must not depend on any domain <c>*Policy</c>.
    /// An adapter serializes a request, calls the external system, and maps the response — deciding
    /// what is allowed is domain / service work.
    /// </summary>
    [Fact]
    public void Client_ShouldNot_DependOn_DomainPolicies()
    {
        Assert.NotEmpty(_domainPolicyFullNames);

        NetArchTest.Rules.TestResult result = Types.InAssembly(_clientAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(_domainPolicyFullNames)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "Maroik.Core.Client must not depend on a domain policy — an external-system adapter " +
            "holds no business rules.\nPolicies: " + string.Join(", ", _domainPolicyFullNames) +
            "\nFailing types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// A client must never call a service (<c>I*Service</c> in Maroik.Core.Contract.Interfaces).
    /// The dependency runs Service → Client only.
    /// </summary>
    [Fact]
    public void Client_ShouldNot_DependOn_ServiceInterfaces()
    {
        Assert.NotEmpty(_serviceInterfaceFullNames);

        NetArchTest.Rules.TestResult result = Types.InAssembly(_clientAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(_serviceInterfaceFullNames)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "Maroik.Core.Client must not depend on a service interface — dependencies flow " +
            "Service → Client, never the reverse.\n" +
            "Failing types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }
}
