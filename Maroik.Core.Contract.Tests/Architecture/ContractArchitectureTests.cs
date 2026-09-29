using System.Reflection;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Primitives;
using NetArchTest.Rules;

namespace Maroik.Core.Contract.Tests.Architecture;

/// <summary>
/// Architecture test enforcing that Maroik.Core.Contract stays a pure abstraction layer:
/// interfaces and DTOs built on Maroik.Core.Domain only, with zero dependency on any concrete
/// implementation layer. Every other layer depends on Contract, so an accidental dependency
/// acquired here (e.g. an EF Core type, or a Website reference) would ripple everywhere.
/// </summary>
public class ContractArchitectureTests
{
    /// <summary>The assembly containing <c>IAccountRepository</c>, referenced by these architecture rules.</summary>
    private static readonly Assembly _contractAssembly = typeof(IAccountRepository).Assembly;

    /// <summary>Contract should not depend on any implementation layer.</summary>
    [Theory]
    [InlineData("Maroik.Core.Repository")]
    [InlineData("Maroik.Core.Client")]
    [InlineData("Maroik.Core.Service")]
    [InlineData("Maroik.Core.PostgreSQL")]
    [InlineData("Maroik.Website")]
    [InlineData("Maroik.Worker")]
    [InlineData("Maroik.FileStorage")]
    public void Contract_ShouldNot_DependOnAnyImplementationLayer(string forbiddenNamespace)
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(_contractAssembly)
            .ShouldNot()
            .HaveDependencyOn(forbiddenNamespace)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"Maroik.Core.Contract must not reference {forbiddenNamespace}. " +
            "Contract is the shared abstraction layer every other layer depends on — it must have " +
            "zero dependency on any concrete implementation.\n" +
            "Failing types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// Contract must be as pure as Domain: the only things it is allowed to reference are the BCL,
    /// the <c>ErrorOr</c> result type (the lightweight shared outcome/error object), and
    /// Maroik.Core.Domain. Interfaces and DTOs here describe the shape of the application's use
    /// cases — pulling in EF Core, ASP.NET Core, a JSON library, DI/hosting abstractions, etc.
    /// would leak that concern into every layer, since every layer depends on Contract.
    /// Mirrors <c>DomainArchitectureTests.Domain_ShouldOnly_DependOn_SystemOrErrorOr</c>.
    /// </summary>
    [Fact]
    public void Contract_ShouldOnly_DependOn_Bcl_ErrorOr_AndDomain()
    {
        AssemblyName[] referenced = _contractAssembly.GetReferencedAssemblies();

        var unexpected = referenced
            .Where(a => a.Name is not null)
            .Where(a => !a.Name!.StartsWith("System", StringComparison.Ordinal))
            .Where(a => !a.Name!.StartsWith("netstandard", StringComparison.Ordinal))
            .Where(a => !a.Name!.StartsWith("Microsoft.CSharp", StringComparison.Ordinal))
            .Where(a => a.Name != "mscorlib")
            .Where(a => a.Name != "ErrorOr")
            .Where(a => a.Name != "Maroik.Core.Domain")
            .Select(a => a.Name)
            .ToList();

        Assert.True(
            unexpected.Count == 0,
            "Maroik.Core.Contract must only depend on the BCL, ErrorOr and Maroik.Core.Domain. " +
            "Unexpected assembly references: " + string.Join(", ", unexpected));
    }

    /// <summary>
    /// Contract must not touch any infrastructure or third-party library. Same list and rationale
    /// as <c>DomainArchitectureTests.Domain_ShouldNot_DependOn_ExternalInfrastructureLibrary</c> —
    /// kept as an explicit per-library check so a regression names the offending library and type,
    /// on top of the assembly-reference check in
    /// <see cref="Contract_ShouldOnly_DependOn_Bcl_ErrorOr_AndDomain"/>.
    /// </summary>
    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("Microsoft.Extensions.DependencyInjection")]
    [InlineData("Microsoft.Extensions.Configuration")]
    [InlineData("Microsoft.Extensions.Logging")]
    [InlineData("Microsoft.Extensions.Options")]
    [InlineData("Microsoft.Extensions.Hosting")]
    [InlineData("Microsoft.Extensions.Caching")]
    [InlineData("Npgsql")]
    [InlineData("MailKit")]
    [InlineData("MimeKit")]
    [InlineData("RabbitMQ.Client")]
    [InlineData("Newtonsoft.Json")]
    [InlineData("HtmlAgilityPack")]
    [InlineData("Ganss.Xss")]
    [InlineData("AngleSharp")]
    [InlineData("ImageMagick")]
    [InlineData("BCrypt.Net")]
    [InlineData("DocumentFormat.OpenXml")]
    [InlineData("ICSharpCode.SharpZipLib")]
    [InlineData("Serilog")]
    [InlineData("Swashbuckle")]
    [InlineData("NonFactors.Grid")]
    [InlineData("Dapper")]
    [InlineData("AutoMapper")]
    [InlineData("MediatR")]
    public void Contract_ShouldNot_DependOn_ExternalInfrastructureLibrary(string forbiddenNamespace)
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(_contractAssembly)
            .ShouldNot()
            .HaveDependencyOn(forbiddenNamespace)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"Maroik.Core.Contract must not reference {forbiddenNamespace}. Contract is a pure " +
            "abstraction layer (interfaces + DTOs over Domain, plus the ErrorOr result type) and " +
            "every other layer depends on it — an infrastructure dependency here leaks everywhere.\n" +
            "Failing types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// Every <c>I*Repository</c> extends <c>IGenericRepository&lt;TDomain&gt;</c>, i.e. it persists
    /// <c>TDomain</c> independently — its own <c>CreateAsync</c> / <c>UpdateEntityAsync</c>, not a
    /// cascade through some parent. In this codebase that is exactly the criterion for a type being
    /// an <see cref="AggregateRoot{TId}"/> rather than a child <see cref="Entity{TId}"/> (see the
    /// root CLAUDE.md and commit 284dc52f). So every <c>TDomain</c> behind a repository interface
    /// must derive from <see cref="AggregateRoot{TId}"/>; a bare <c>Entity&lt;TId&gt;</c> with its
    /// own repository is a modeling bug.
    /// </summary>
    [Fact]
    public void EveryRepositoryInterface_MustPersist_AnAggregateRoot()
    {
        (Type Repo, Type Domain)[] persisted =
        [
            .. _contractAssembly.GetTypes()
                .Where(t => t is { IsInterface: true, Namespace: "Maroik.Core.Contract.Interfaces" })
                .SelectMany(
                    repo => repo.GetInterfaces()
                        .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IGenericRepository<>))
                        .Select(i => (Repo: repo, Domain: i.GetGenericArguments()[0])))
        ];

        Assert.NotEmpty(persisted);

        string[] violations =
        [
            .. persisted
                .Where(p => !DerivesFromAggregateRoot(p.Domain))
                .Select(p => $"{p.Repo.Name} persists {p.Domain.Name}, which is {DescribeBase(p.Domain)}")
        ];

        Assert.True(
            violations.Length == 0,
            "Every type persisted through its own repository must be an AggregateRoot<TId>, not a " +
            "child Entity<TId>:\n" + string.Join("\n", violations));
    }

    /// <summary>True when any base type of <paramref name="type"/> is <c>AggregateRoot&lt;TId&gt;</c>.</summary>
    private static bool DerivesFromAggregateRoot(Type type)
    {
        for (Type? t = type.BaseType; t is not null; t = t.BaseType)
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(AggregateRoot<>))
                return true;
        return false;
    }

    /// <summary>Explains, for a failure message, why <paramref name="type"/> is not an aggregate root.</summary>
    private static string DescribeBase(Type type)
    {
        for (Type? t = type.BaseType; t is not null; t = t.BaseType)
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Entity<>))
                return "a bare Entity<TId> (should be AggregateRoot<TId>)";
        return "not a domain entity at all";
    }
}
