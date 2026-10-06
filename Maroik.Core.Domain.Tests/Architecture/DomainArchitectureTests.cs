using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NetArchTest.Rules;
namespace Maroik.Core.Domain.Tests.Architecture;

/// <summary>
/// Architecture tests enforcing that Maroik.Core.Domain is the innermost layer of the
/// application: it must not depend on any outer layer (Contract, Client, Repository,
/// Service, PostgreSQL, Website, Worker, FileStorage), and it must not depend on any
/// framework other than the BCL and the ErrorOr result-type library.
/// </summary>
public class DomainArchitectureTests
{
    /// <summary>The Maroik.Core.Domain assembly inspected by these architecture rules.</summary>
    private static readonly Assembly _domainAssembly = typeof(Domain.Account.Account).Assembly;

    /// <summary>Domain should not depend on any other layer.</summary>
    [Theory]
    [InlineData("Maroik.Core.Contract")]
    [InlineData("Maroik.Core.Client")]
    [InlineData("Maroik.Core.Repository")]
    [InlineData("Maroik.Core.Service")]
    [InlineData("Maroik.Core.PostgreSQL")]
    [InlineData("Maroik.Website")]
    [InlineData("Maroik.Worker")]
    [InlineData("Maroik.FileStorage")]
    public void Domain_ShouldNot_DependOnAnyOtherLayer(string forbiddenNamespace)
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(_domainAssembly)
            .ShouldNot()
            .HaveDependencyOn(forbiddenNamespace)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"Maroik.Core.Domain must not reference {forbiddenNamespace}. " +
            "The domain layer must have zero outward dependencies so it can be reused by " +
            "every other layer without creating a cycle.\n" +
            "Failing types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>Domain should only depend on system or error or.</summary>
    [Fact]
    public void Domain_ShouldOnly_DependOn_SystemOrErrorOr()
    {
        AssemblyName[] referenced = _domainAssembly.GetReferencedAssemblies();

        var unexpected = referenced
            .Where(a => a.Name is not null)
            .Where(a => !a.Name!.StartsWith("System", StringComparison.Ordinal))
            .Where(a => !a.Name!.StartsWith("netstandard", StringComparison.Ordinal))
            .Where(a => a.Name != "ErrorOr")
            .Select(a => a.Name)
            .ToList();

        Assert.True(
            unexpected.Count == 0,
            "Maroik.Core.Domain must only depend on the BCL and ErrorOr. " +
            "Unexpected assembly references: " + string.Join(", ", unexpected));
    }

    /// <summary>
    /// Domain must not touch any infrastructure or third-party library used elsewhere in the
    /// solution (EF Core, ASP.NET Core, MailKit, RabbitMQ, JSON/HTML/image libraries, DI and
    /// hosting abstractions, …). Business logic in the innermost layer stays framework-free; the
    /// single deliberate exception is <c>ErrorOr</c>, the lightweight <c>ErrorOr&lt;T&gt;</c> /
    /// <c>Error</c> result type used to report outcomes without throwing. This overlaps
    /// <see cref="Domain_ShouldOnly_DependOn_SystemOrErrorOr"/> (an assembly-reference check); it is
    /// kept separately so a violation names the exact library and the exact offending type.
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
    public void Domain_ShouldNot_DependOn_ExternalInfrastructureLibrary(string forbiddenNamespace)
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(_domainAssembly)
            .ShouldNot()
            .HaveDependencyOn(forbiddenNamespace)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"Maroik.Core.Domain must not reference {forbiddenNamespace}. The domain layer must " +
            "stay pure: business rules, invariants and policies are plain C# with no framework or " +
            "third-party dependency (the sole exception is the ErrorOr result type).\n" +
            "Failing types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// Bounded contexts recognized within Maroik.Core.Domain. Each one models a distinct
    /// area of the business (Account, Board, Calendar, Finance, Menu) and must stay
    /// independent of the others — cross-context collaboration, if ever needed, should go
    /// through an explicit integration pattern (e.g. a loosely-coupled reference such as
    /// Board.Writer being a plain string instead of an Account reference) rather than a
    /// direct type dependency.
    /// </summary>
    private static readonly string[] _boundedContextNamespaces =
    [
        "Maroik.Core.Domain.Account",
        "Maroik.Core.Domain.Board",
        "Maroik.Core.Domain.Calendar",
        "Maroik.Core.Domain.Finance",
        "Maroik.Core.Domain.Menu"
    ];

    /// <summary>Bounded context should not depend on another bounded context.</summary>
    [Theory]
    [InlineData("Maroik.Core.Domain.Account")]
    [InlineData("Maroik.Core.Domain.Board")]
    [InlineData("Maroik.Core.Domain.Calendar")]
    [InlineData("Maroik.Core.Domain.Finance")]
    [InlineData("Maroik.Core.Domain.Menu")]
    public void BoundedContext_ShouldNot_DependOnAnotherBoundedContext(string boundedContextNamespace)
    {
        string[] otherContexts =
        [
            .. _boundedContextNamespaces
                .Where(ns => ns != boundedContextNamespace)
        ];

        NetArchTest.Rules.TestResult result = Types.InAssembly(_domainAssembly)
            .That()
            .ResideInNamespace(boundedContextNamespace)
            .ShouldNot()
            .HaveDependencyOnAny(otherContexts)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"Types in {boundedContextNamespace} must not depend on another bounded context " +
            $"({string.Join(", ", otherContexts)}). Bounded contexts must stay independent; " +
            "use a loosely-coupled reference (e.g. a plain identifier/string) instead of a " +
            "direct type dependency if cross-context data is needed.\n" +
            "Failing types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// The domain is pure (CLAUDE.md → "Logging"): it returns errors, it does not log. Logging belongs to
    /// the service / client / host layers that decide what an error means.
    /// </summary>
    [Fact]
    public void Domain_ShouldNot_Log()
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(_domainAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.Extensions.Logging")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Maroik.Core.Domain must not depend on Microsoft.Extensions.Logging.\n" +
            "Failing types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// Every Domain error message must be built via <c>LocalizableError.Validation</c>/<c>Conflict</c>/
    /// <c>Failure</c>, never the raw <c>ErrorOr.Error</c> factories directly. <c>LocalizableError</c> is
    /// what attaches the composite-format template + args to <c>Error.Metadata</c> so the UI can
    /// localize the message via resx instead of falling back to an already-baked, unlocalizable
    /// sentence (see <c>Maroik.Core.Domain.Localization.LocalizableError</c>'s doc comment). This was
    /// previously true for only 6 of 104 Domain call sites — the rest called <c>Error.Validation</c>/
    /// <c>Conflict</c>/<c>Failure</c> directly, and for <see cref="Maroik.Core.Domain.ValueObjects.Money"/>'s
    /// currency-mismatch errors that bug was live: a genuinely runtime currency code was baked straight
    /// into <c>Error.Description</c> with no template, so the resx localizer could never match it.
    /// </summary>
    [Fact]
    public void Domain_ErrorMessages_MustGoThrough_LocalizableError()
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(_domainAssembly)
            .That()
            .DoNotResideInNamespace("Maroik.Core.Domain.Localization")
            .Should()
            .MeetCustomRule(new DoesNotCallRawErrorFactory())
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "Domain types must build errors via LocalizableError.Validation/Conflict/Failure, not the " +
            "raw ErrorOr.Error.Validation/Conflict/Failure/NotFound/Forbidden/Unexpected factories " +
            "directly — only Maroik.Core.Domain.Localization.LocalizableError is allowed to call those.\n" +
            "Failing types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// The domain never reads the clock (<c>DateTime.UtcNow</c>/<c>Now</c>/<c>Today</c>,
    /// <c>DateTimeOffset.UtcNow</c>/<c>Now</c>) and never holds a <see cref="TimeProvider"/>: the
    /// current time is a <c>DateTime utcNow</c> argument that the calling use case reads once from its
    /// injected <see cref="TimeProvider"/>, so expiry and boundary rules are testable at exact instants
    /// and one operation cannot stamp two different times.
    /// </summary>
    [Fact]
    public void Domain_ShouldNot_ReadTheClock()
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(_domainAssembly)
            .Should()
            .MeetCustomRule(new DoesNotReadTheClock())
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Maroik.Core.Domain must take the current time as a DateTime argument instead of reading " +
            "DateTime/DateTimeOffset.UtcNow/Now/Today or using TimeProvider.\n" +
            "Failing types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// <paramref name="type"/> and every type nested in it. NetArchTest hands a custom rule only the
    /// top-level type, but lambdas, iterators and async methods compile their bodies into nested types,
    /// so an IL rule that looks only at <c>type.Methods</c> silently misses them.
    /// </summary>
    private static IEnumerable<TypeDefinition> Flatten(TypeDefinition type)
        => [type, .. type.NestedTypes.SelectMany(Flatten)];

    /// <summary>
    /// Flags a type whose IL reads the system clock through <see cref="DateTime"/> /
    /// <see cref="DateTimeOffset"/> or calls into <see cref="TimeProvider"/>.
    /// </summary>
    private sealed class DoesNotReadTheClock : ICustomRule
    {
        /// <summary>The clock-reading property getters, as "declaring type::method".</summary>
        private static readonly string[] _clockGetters =
        [
            "System.DateTime::get_UtcNow", "System.DateTime::get_Now", "System.DateTime::get_Today",
            "System.DateTimeOffset::get_UtcNow", "System.DateTimeOffset::get_Now",
        ];

        /// <summary>Returns <see langword="false"/> when any method body of <paramref name="type"/> (or a type nested in it) reads the clock.</summary>
        public bool MeetsRule(TypeDefinition type)
            => !Flatten(type).SelectMany(t => t.Methods).Where(method => method.HasBody)
                .SelectMany(method => method.Body.Instructions)
                .Any(instruction => instruction.Operand is MethodReference called
                    && (_clockGetters.Contains($"{called.DeclaringType.FullName}::{called.Name}")
                        || called.DeclaringType.FullName == "System.TimeProvider"));
    }

    /// <summary>
    /// Flags a type whose IL directly calls one of the raw <c>ErrorOr.Error</c> factory methods.
    /// Deliberately checked at the method-call level via Mono.Cecil rather than as a type-dependency
    /// (<c>HaveDependencyOn("ErrorOr.Error")</c>) rule: every Domain type legitimately references the
    /// <c>ErrorOr&lt;T&gt;</c> return type, whose full name (<c>ErrorOr.ErrorOr`1</c>) shares the
    /// <c>"ErrorOr.Error"</c> prefix, so a dependency-based check would also flag that unrelated,
    /// required reference.
    /// </summary>
    private sealed class DoesNotCallRawErrorFactory : ICustomRule
    {
        /// <summary>The raw <c>ErrorOr.Error</c> factory methods domain code must not call directly.</summary>
        private static readonly string[] _rawFactoryMethodNames =
            ["Validation", "Conflict", "Failure", "NotFound", "Forbidden", "Unexpected"];

        /// <summary>Returns <see langword="false"/> when any method body of <paramref name="type"/> (or a type nested in it) calls a raw <c>ErrorOr.Error</c> factory.</summary>
        public bool MeetsRule(TypeDefinition type)
        {
            foreach (var instruction in Flatten(type).SelectMany(t => t.Methods).Where(method => method.HasBody).SelectMany(method => method.Body.Instructions.Where(instruction => instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt)))
            {
                if (instruction.Operand is not MethodReference calledMethod) continue;

                if (calledMethod.DeclaringType.FullName == "ErrorOr.Error"
                    && _rawFactoryMethodNames.Contains(calledMethod.Name))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
