using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Maroik.Core.Contract.Interfaces;
using Maroik.Website.Attributes;
using Maroik.Website.Filters;
using Microsoft.AspNetCore.Mvc;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NetArchTest.Rules;

namespace Maroik.Website.Tests.Architecture;

/// <summary>
/// Architecture tests that enforce the layering rule for Maroik.Website:
///
///   Website  →  Service interfaces (Maroik.Core.Contract)
///                    ↓
///              Service implementations (Maroik.Core.Service)
///                    ↓
///              Repository + Client implementations
///
/// Website must only consume the service layer via interfaces declared in
/// Maroik.Core.Contract. Direct dependencies on Maroik.Core.Repository,
/// Maroik.Core.Client, or Maroik.Core.Service (the implementations) are
/// forbidden for all types except Program (the composition root), which
/// wires up implementations into the DI container.
/// </summary>
public class WebsiteArchitectureTests
{
    /// <summary>The assembly containing <c>ViewBagPopulatorFilter</c>, referenced by these architecture rules.</summary>
    private static readonly Assembly _websiteAssembly = typeof(ViewBagPopulatorFilter).Assembly;
    /// <summary>The assembly containing <c>IAccountRepository</c>, referenced by these architecture rules.</summary>
    private static readonly Assembly _contractAssembly = typeof(IAccountRepository).Assembly;

    /// <summary>Root namespace of the Repository layer.</summary>
    private const string RepositoryNamespace = "Maroik.Core.Repository";
    /// <summary>Root namespace of the external-system clients.</summary>
    private const string ClientNamespace = "Maroik.Core.Client";
    /// <summary>Root namespace of the Service layer.</summary>
    private const string ServiceNamespace = "Maroik.Core.Service";
    /// <summary>Root namespace of the EF Core persistence model.</summary>
    private const string PostgreSqlNamespace = "Maroik.Core.PostgreSQL";

    /// <summary>
    /// Full names of every repository interface (<c>I*Repository</c>) declared in
    /// Maroik.Core.Contract.Interfaces. Namespace-based checks alone don't stop a Website type
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

    /// <summary>Website types except composition root should not depend on repository namespace.</summary>
    [Fact]
    public void WebsiteTypes_ExceptCompositionRoot_ShouldNot_DependOnRepositoryNamespace()
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(_websiteAssembly)
            .That()
            .DoNotHaveName("Program")
            .ShouldNot()
            .HaveDependencyOn(RepositoryNamespace)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "Non-composition-root types in Maroik.Website must not reference " +
            "Maroik.Core.Repository directly. " +
            "Use service interfaces from Maroik.Core.Contract instead.\n" +
            "Failing types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// Website types except composition root should not depend on repository interfaces,
    /// even though those interfaces are declared in Maroik.Core.Contract (not
    /// Maroik.Core.Repository). Only Maroik.Core.Service may consume them.
    /// </summary>
    [Fact]
    public void WebsiteTypes_ExceptCompositionRoot_ShouldNot_DependOnRepositoryInterfaces()
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(_websiteAssembly)
            .That()
            .DoNotHaveName("Program")
            .ShouldNot()
            .HaveDependencyOnAny(_repositoryInterfaceFullNames)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "Non-composition-root types in Maroik.Website must not depend on repository " +
            "interfaces (I*Repository) directly. Even though these are declared in " +
            "Maroik.Core.Contract, only Maroik.Core.Service may consume them — inject a " +
            "service interface instead.\n" +
            "Failing types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>Website types except composition root should not depend on client namespace.</summary>
    [Fact]
    public void WebsiteTypes_ExceptCompositionRoot_ShouldNot_DependOnClientNamespace()
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(_websiteAssembly)
            .That()
            .DoNotHaveName("Program")
            .ShouldNot()
            .HaveDependencyOn(ClientNamespace)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "Non-composition-root types in Maroik.Website must not reference " +
            "Maroik.Core.Client directly. " +
            "Use service interfaces from Maroik.Core.Contract instead.\n" +
            "Failing types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>Website types except composition root should not depend on service namespace.</summary>
    [Fact]
    public void WebsiteTypes_ExceptCompositionRoot_ShouldNot_DependOnServiceNamespace()
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(_websiteAssembly)
            .That()
            .DoNotHaveName("Program")
            .ShouldNot()
            .HaveDependencyOn(ServiceNamespace)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "Non-composition-root types in Maroik.Website must not reference " +
            "Maroik.Core.Service implementations directly. " +
            "Use service interfaces from Maroik.Core.Contract instead.\n" +
            "Failing types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// No type in Maroik.Website, including Program, should depend on Maroik.Core.PostgreSQL.
    /// Website does not even project-reference the PostgreSQL model project; the composition root
    /// registers EF Core via <c>AddRepositoryContext</c> in Maroik.Core.Repository. Mirrors
    /// <c>WorkerArchitectureTests.WorkerTypes_ShouldNot_DependOnPostgreSqlNamespace</c>.
    /// </summary>
    [Fact]
    public void WebsiteTypes_ShouldNot_DependOnPostgreSqlNamespace()
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(_websiteAssembly)
            .That()
            .ResideInNamespaceStartingWith("Maroik.Website")
            .ShouldNot()
            .HaveDependencyOn(PostgreSqlNamespace)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "Maroik.Website must not reference Maroik.Core.PostgreSQL directly, not even from " +
            "Program. Register EF Core via AddRepositoryContext in Maroik.Core.Repository instead.\n" +
            "Failing types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>Controllers should not depend on repository or client or service namespace.</summary>
    [Fact]
    public void Controllers_ShouldNot_DependOnRepositoryOrClientOrServiceNamespace()
    {
        NetArchTest.Rules.TestResult repoResult = Types.InAssembly(_websiteAssembly)
            .That()
            .ResideInNamespace("Maroik.Website.Controllers")
            .ShouldNot()
            .HaveDependencyOn(RepositoryNamespace)
            .GetResult();

        NetArchTest.Rules.TestResult clientResult = Types.InAssembly(_websiteAssembly)
            .That()
            .ResideInNamespace("Maroik.Website.Controllers")
            .ShouldNot()
            .HaveDependencyOn(ClientNamespace)
            .GetResult();

        NetArchTest.Rules.TestResult serviceResult = Types.InAssembly(_websiteAssembly)
            .That()
            .ResideInNamespace("Maroik.Website.Controllers")
            .ShouldNot()
            .HaveDependencyOn(ServiceNamespace)
            .GetResult();

        Assert.True(
            repoResult.IsSuccessful,
            "Controllers must not reference Maroik.Core.Repository directly. " +
            "Inject IXxxService interfaces instead.\n" +
            "Failing types: " + string.Join(", ", repoResult.FailingTypeNames ?? []));

        Assert.True(
            clientResult.IsSuccessful,
            "Controllers must not reference Maroik.Core.Client directly. " +
            "Inject IXxxService interfaces instead.\n" +
            "Failing types: " + string.Join(", ", clientResult.FailingTypeNames ?? []));

        Assert.True(
            serviceResult.IsSuccessful,
            "Controllers must not reference Maroik.Core.Service implementations directly. " +
            "Inject IXxxService interfaces from Maroik.Core.Contract instead.\n" +
            "Failing types: " + string.Join(", ", serviceResult.FailingTypeNames ?? []));
    }

    /// <summary>
    /// Controllers must not declare non-public helper methods. Every method the routing system can
    /// dispatch to must be a fully public action, because the set of Controller/Action pairs the app
    /// exposes is exactly what's driven by the Category/SubCategory rows seeded in Init.sql (which
    /// AuthorizationFilter matches against for GET requests) and the [RequiredHttpPostAccess]
    /// attribute (for POST). A private helper method can't itself become a route, but it blurs that
    /// "every controller method is a seeded action" invariant and invites logic creep into the
    /// controller. Shared logic across actions belongs in a service or a Website/Extensions method
    /// instead (see CalendarEventViewModelExtensions.GetCalendarEventDetailViewModelAsync).
    /// </summary>
    [Fact]
    public void Controllers_ShouldNot_DeclareNonPublicMethods()
    {
        List<string> violations =
        [
            .. from controllerType in _websiteAssembly.GetTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(Controller).IsAssignableFrom(t))
            let nonPublicMethods = (List<string>)
            [
                .. controllerType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Where(m => !m.IsSpecialName) // exclude property/event accessors
                    .Where(m => m.GetCustomAttribute<CompilerGeneratedAttribute>() == null) // exclude lambda bodies etc.
                    .Select(m => m.Name)
            ]
            where nonPublicMethods.Count > 0
            select $"{controllerType.Name}: {string.Join(", ", nonPublicMethods)}"

        ];

        Assert.True(
            violations.Count == 0,
            "Controllers must not declare non-public helper methods — move shared logic into a " +
            "service or a Website/Extensions method instead.\n" +
            "Violations: " + string.Join("; ", violations));
    }

    /// <summary>
    /// Every POST action a controller exposes must be self-describing to <see cref="AuthorizationFilter"/>:
    ///
    /// <list type="bullet">
    ///   <item>It needs <c>[ValidateAntiForgeryToken]</c> — the marker <c>AuthorizationFilter.HasPostGuards</c>
    ///   keys on. A POST action without it is silently dropped from both the role map and the
    ///   anonymous allow-list, i.e. unreachable.</item>
    ///   <item>Unless it lives on <c>AccountController</c> (the anonymous self-service surface, handled
    ///   by <c>AuthorizationFilter.AnonymousAccountPostActions</c>), it needs at least one
    ///   <c>[RequiredHttpPostAccess(Role = ...)]</c> with a non-empty role — without it the action
    ///   appears in no role's <c>PostActionRoles</c> entry and every logged-in caller is denied.</item>
    /// </list>
    ///
    /// This is a build-time backstop: the runtime failure mode is a silent "redirect to Dashboard",
    /// which is easy to miss when adding a new POST action.
    /// </summary>
    [Fact]
    public void PostActions_MustCarry_AntiforgeryAndRoleAttributes()
    {
        List<string> violations = [];

        foreach (Type controllerType in _websiteAssembly.GetTypes()
                     .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(Controller).IsAssignableFrom(t)))
        {
            bool isAnonymousSelfServiceController = controllerType.Name == "AccountController";

            foreach (MethodInfo method in controllerType.GetMethods(
                         BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (method.IsDefined(typeof(NonActionAttribute)) || !method.IsDefined(typeof(HttpPostAttribute)))
                    continue;

                if (!method.IsDefined(typeof(ValidateAntiForgeryTokenAttribute)))
                    violations.Add($"{controllerType.Name}.{method.Name}: missing [ValidateAntiForgeryToken]");

                if (isAnonymousSelfServiceController)
                    continue;

                List<RequiredHttpPostAccessAttribute> roleAttributes =
                    [.. method.GetCustomAttributes<RequiredHttpPostAccessAttribute>()];

                if (roleAttributes.Count == 0)
                    violations.Add($"{controllerType.Name}.{method.Name}: missing [RequiredHttpPostAccess]");
                else if (roleAttributes.All(a => string.IsNullOrEmpty(a.Role)))
                    violations.Add($"{controllerType.Name}.{method.Name}: [RequiredHttpPostAccess] has no non-empty Role");
            }
        }

        Assert.True(
            violations.Count == 0,
            "Every controller POST action must declare [ValidateAntiForgeryToken] and — outside " +
            "AccountController — at least one [RequiredHttpPostAccess(Role = ...)]. AuthorizationFilter " +
            "silently denies any POST action that doesn't.\nViolations:\n  " + string.Join("\n  ", violations));
    }

    /// <summary>
    /// Controllers must be declared as a single, non-partial class — a large controller must not be
    /// split across multiple files via the <c>partial</c> keyword. This can't be checked the way the
    /// other rules in this file are: the C# compiler merges every partial declaration into one type
    /// before it's ever emitted to the assembly, so by the time NetArchTest/reflection can see the
    /// type, "was this partial, and across how many files" information no longer exists. This test
    /// scans the actual .cs source files instead of the compiled assembly for that reason.
    /// </summary>
    [Fact]
    public void ControllerFiles_ShouldNot_BeDeclaredAsPartialClasses()
    {
        string controllersDirectory = Path.Combine(FindRepositoryRoot(), "Maroik.Website", "Controllers");

        List<string> offendingFiles =
        [
            .. Directory.GetFiles(controllersDirectory, "*.cs", SearchOption.AllDirectories)
 #pragma warning disable SYSLIB1045
                .Where(path => Regex.IsMatch(File.ReadAllText(path), @"\bpartial\s+class\s+\w*Controller\b"))
 #pragma warning restore SYSLIB1045
        ];

        Assert.True(
            offendingFiles.Count == 0,
            "A controller must be declared as a single, non-partial class — do not split a large " +
            "controller across multiple files via 'partial'. Extract shared logic to a service or " +
            "a Website/Extensions method instead, or split into separate controllers if the actions " +
            "are genuinely independent.\n" +
            "Offending files: " + string.Join(", ", offendingFiles));
    }

    /// <summary>
    /// Controllers must read the currently logged-in account via <c>HttpContext.GetLoggedInAccount()</c> —
    /// re-fetched from the database by <see cref="ViewBagPopulatorFilter"/> on every request — never
    /// <c>ISessionService.GetAccount()</c>, which returns the snapshot cached in the session at
    /// login time. A mid-session change to Role, Locked, Nickname, TimeZoneIanaId, etc. must take
    /// effect on the very next request; reading the stale session copy instead let a demoted admin
    /// keep an admin's view of a locked post until they logged out and back in (the bug this rule
    /// exists to prevent a recurrence of). <c>SetAccount</c>/<c>RemoveAccount</c> (the write side —
    /// login/logout/profile-refresh) are unaffected; only the <c>GetAccount</c> read is forbidden.
    /// </summary>
    [Fact]
    public void Controllers_ShouldNot_CallSessionServiceGetAccount()
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(_websiteAssembly)
            .That()
            .ResideInNamespace("Maroik.Website.Controllers")
            .Should()
            .MeetCustomRule(new DoesNotCallSessionServiceGetAccount())
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "Controllers must read the logged-in account via HttpContext.GetLoggedInAccount() (populated " +
            "fresh from the database by ViewBagPopulatorFilter on every request), not " +
            "ISessionService.GetAccount() (the stale, login-time session snapshot). " +
            "SetAccount/RemoveAccount are still allowed — only the GetAccount read is forbidden.\n" +
            "Failing types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// The logged-in account lives on <c>HttpContext.Items</c> and is read through the typed
    /// <c>HttpContext.GetLoggedInAccount()</c> extension. Nothing in the
    /// Website assembly — controllers, filters, or the Razor views compiled into it — may read or write it
    /// as the dynamic <c>ViewBag.LoggedInAccount</c> again: that bypasses the compiler (a typo or wrong cast
    /// only fails at runtime) and splits the account across two sources that can drift apart.
    /// </summary>
    [Fact]
    public void WebsiteTypes_ShouldNot_AccessLoggedInAccountThroughViewBag()
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(_websiteAssembly)
            .Should()
            .MeetCustomRule(new DoesNotAccessDynamicMember("LoggedInAccount"))
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "Read the logged-in account via HttpContext.GetLoggedInAccount() (Context.GetLoggedInAccount() in " +
            "views), not the dynamic ViewBag.LoggedInAccount.\n" +
            "Failing types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// <paramref name="type"/> and every type nested in it. NetArchTest hands a custom rule only the
    /// top-level type, but async methods (every Razor view's <c>ExecuteAsync</c>, most controller actions),
    /// lambdas and iterators compile their bodies into nested types — so an IL rule that looks only at
    /// <c>type.Methods</c> silently misses them. Mirrors <c>DomainArchitectureTests.DoesNotReadTheClock.Flatten</c>.
    /// </summary>
    private static IEnumerable<TypeDefinition> WithNestedTypes(TypeDefinition type)
        => [type, .. type.NestedTypes.SelectMany(WithNestedTypes)];

    /// <summary>
    /// Flags a type whose IL accesses a member with the given name dynamically (e.g. <c>ViewBag.X</c>).
    /// The C# compiler lowers each dynamic get/set to a call site created by
    /// <c>Microsoft.CSharp.RuntimeBinder.Binder.GetMember/SetMember(flags, "X", ...)</c>, with the member
    /// name as the last string literal loaded before that call — so that pair identifies the access
    /// without matching unrelated string literals or the typed <c>LoggedInAccount</c> view-model properties.
    /// </summary>
    private sealed class DoesNotAccessDynamicMember(string memberName) : ICustomRule
    {
        /// <summary>Returns <see langword="false"/> when any method body of <paramref name="type"/> (or a type nested in it) binds <c>memberName</c> dynamically.</summary>
        public bool MeetsRule(TypeDefinition type)
        {
            foreach (var method in WithNestedTypes(type).SelectMany(t => t.Methods).Where(method => method.HasBody))
            {
                string? lastStringLiteral = null;
                foreach (var instruction in method.Body.Instructions)
                {
                    if (instruction.OpCode == OpCodes.Ldstr)
                    {
                        lastStringLiteral = (string)instruction.Operand;
                    }
                    else if (instruction.OpCode == OpCodes.Call
                        && instruction.Operand is MethodReference calledMethod
                        && calledMethod.DeclaringType.FullName == "Microsoft.CSharp.RuntimeBinder.Binder"
                        && (calledMethod.Name is "GetMember" or "SetMember")
                        && lastStringLiteral == memberName)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Flags a type whose IL directly calls <c>ISessionService.GetAccount</c>. Checked at the
    /// method-call level via Mono.Cecil (not a type-dependency rule) because controllers
    /// legitimately depend on <c>ISessionService</c> itself for <c>SetAccount</c>/<c>RemoveAccount</c>
    /// — only the <c>GetAccount</c> read is forbidden. Mirrors
    /// <c>DomainArchitectureTests.DoesNotCallRawErrorFactory</c>'s approach.
    /// </summary>
    private sealed class DoesNotCallSessionServiceGetAccount : ICustomRule
    {
        /// <summary>Returns <see langword="false"/> when any method body of <paramref name="type"/> (or a type nested in it) calls <c>ISessionService.GetAccount</c>.</summary>
        public bool MeetsRule(TypeDefinition type)
        {
            foreach (var instruction in WithNestedTypes(type).SelectMany(t => t.Methods).Where(method => method.HasBody).SelectMany(method => method.Body.Instructions.Where(instruction => instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt)))
            {
                if (instruction.Operand is not MethodReference calledMethod) continue;

                if (calledMethod.DeclaringType.FullName == "Maroik.Website.Contracts.ISessionService"
                    && calledMethod.Name == "GetAccount")
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>Walks up from the test assembly's output directory to find the repository root (identified by Maroik.sln).</summary>
    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Maroik.sln")))
            dir = dir.Parent;
        return dir == null ? throw new InvalidOperationException("Could not locate the repository root (Maroik.sln) from " + AppContext.BaseDirectory) : dir.FullName;
    }

    /// <summary>
    /// Razor views are compiled into classes under the <c>AspNetCoreGeneratedDocument</c>
    /// namespace. Views must only render the ViewModel/DTO already handed to them by the
    /// controller — they must not reach into Repository, Client, or Service implementations
    /// (e.g. via <c>@inject</c>) to fetch or mutate data themselves.
    /// </summary>
    [Fact]
    public void Views_ShouldNot_DependOnRepositoryOrClientOrServiceNamespace()
    {
        const string generatedViewNamespace = "AspNetCoreGeneratedDocument";

        // NetArchTest passes vacuously when the selection is empty, so prove the compiled Razor
        // views really are in the assembly (otherwise this rule would silently check nothing).
        Assert.NotEmpty(Types.InAssembly(_websiteAssembly)
            .That()
            .ResideInNamespace(generatedViewNamespace)
            .GetTypes());

        NetArchTest.Rules.TestResult repoResult = Types.InAssembly(_websiteAssembly)
            .That()
            .ResideInNamespace(generatedViewNamespace)
            .ShouldNot()
            .HaveDependencyOn(RepositoryNamespace)
            .GetResult();

        NetArchTest.Rules.TestResult clientResult = Types.InAssembly(_websiteAssembly)
            .That()
            .ResideInNamespace(generatedViewNamespace)
            .ShouldNot()
            .HaveDependencyOn(ClientNamespace)
            .GetResult();

        NetArchTest.Rules.TestResult serviceResult = Types.InAssembly(_websiteAssembly)
            .That()
            .ResideInNamespace(generatedViewNamespace)
            .ShouldNot()
            .HaveDependencyOn(ServiceNamespace)
            .GetResult();

        Assert.True(
            repoResult.IsSuccessful,
            "Views must not reference Maroik.Core.Repository directly (e.g. via @inject). " +
            "Pass all data through the controller's ViewModel instead.\n" +
            "Failing types: " + string.Join(", ", repoResult.FailingTypeNames ?? []));

        Assert.True(
            clientResult.IsSuccessful,
            "Views must not reference Maroik.Core.Client directly (e.g. via @inject). " +
            "Pass all data through the controller's ViewModel instead.\n" +
            "Failing types: " + string.Join(", ", clientResult.FailingTypeNames ?? []));

        Assert.True(
            serviceResult.IsSuccessful,
            "Views must not reference Maroik.Core.Service implementations directly (e.g. via @inject). " +
            "Pass all data through the controller's ViewModel instead.\n" +
            "Failing types: " + string.Join(", ", serviceResult.FailingTypeNames ?? []));
    }
}
