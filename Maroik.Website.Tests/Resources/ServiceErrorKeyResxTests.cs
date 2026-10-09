using System.Reflection;
using System.Xml.Linq;
using Maroik.Core.Contract.Dtos;
using Maroik.Core.Service.Services;
using Maroik.Website.Controllers;
using Microsoft.AspNetCore.Mvc;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Maroik.Website.Tests.Resources;

/// <summary>
/// A controller relays a failed service result as <c>localizer[result.ErrorKey, result.ErrorArgs]</c>, so every
/// <see cref="ServiceErrorKeys"/> value a controller can receive must be a key of that controller's resx pair — otherwise Korean
/// visitors get the English key text back. <c>ResourceCompletenessTests</c> only sees the literal keys written in the controller
/// itself; this test follows the calls instead: from each controller's methods into the service methods it calls (through their
/// Contract interface), and on through the private helpers, lambdas, async state machines and other services those call, collecting
/// the <see cref="ServiceErrorKeys"/> values reached on the way (a <c>const</c> is compiled to the <c>ldstr</c> of its value).
/// </summary>
public class ServiceErrorKeyResxTests
{
    /// <summary>The Maroik.Website project folder.</summary>
    private static readonly string _websiteDir = FindWebsiteDirectory();

    /// <summary>Every top-level <see cref="ServiceErrorKeys"/> value (the <see cref="ServiceErrorKeys.Signals"/> are not resx keys).</summary>
    private static readonly HashSet<string> _keys = typeof(ServiceErrorKeys)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral)
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToHashSet();

    /// <summary>Resolves the Maroik assemblies from the test's own output folder.</summary>
    private static readonly ReaderParameters _readerParameters = CreateReaderParameters();

    /// <summary>Maroik.Core.Service, read with Cecil.</summary>
    private static readonly ModuleDefinition _serviceModule = ModuleDefinition.ReadModule(typeof(AccountService).Assembly.Location, _readerParameters);

    /// <summary>Maroik.Website, read with Cecil.</summary>
    private static readonly ModuleDefinition _websiteModule = ModuleDefinition.ReadModule(typeof(AccountController).Assembly.Location, _readerParameters);

    /// <summary>The name of the assembly holding the service interfaces.</summary>
    private static readonly string _contractAssemblyName = typeof(ServiceErrorKeys).Assembly.GetName().Name!;

    /// <summary>Reader parameters whose resolver searches the test's output folder.</summary>
    private static ReaderParameters CreateReaderParameters()
    {
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(AppContext.BaseDirectory);
        return new ReaderParameters { AssemblyResolver = resolver };
    }

    /// <summary>Walks up to the directory holding <c>Maroik.sln</c> and returns its <c>Maroik.Website</c> subfolder.</summary>
    private static string FindWebsiteDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Maroik.sln"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Could not locate Maroik.sln"), "Maroik.Website");
    }

    /// <summary>A type and every type nested in it (compiler-generated state machines and closures included).</summary>
    private static IEnumerable<TypeDefinition> Flatten(TypeDefinition type) => [type, .. type.NestedTypes.SelectMany(Flatten)];

    /// <summary>The Maroik.Core.Service methods that implement <paramref name="interfaceMethod"/> (a Contract interface method).</summary>
    private static IEnumerable<MethodDefinition> Implementations(MethodReference interfaceMethod)
    {
        string interfaceName = interfaceMethod.DeclaringType.FullName;
        return _serviceModule.Types
            .Where(t => t.Interfaces.Any(i => i.InterfaceType.FullName == interfaceName))
            .SelectMany(t => t.Methods)
            .Where(m => m.Name == interfaceMethod.Name &&
                        m.Parameters.Select(p => p.ParameterType.FullName)
                            .SequenceEqual(interfaceMethod.Parameters.Select(p => p.ParameterType.FullName)));
    }

    /// <summary>The <c>MoveNext</c> of the state machine behind an async / iterator method, when it is one.</summary>
    private static IEnumerable<MethodDefinition> StateMachineBody(MethodDefinition method) =>
        method.CustomAttributes
            .Where(a => a.AttributeType.FullName is "System.Runtime.CompilerServices.AsyncStateMachineAttribute"
                or "System.Runtime.CompilerServices.IteratorStateMachineAttribute")
            .Select(a => ((TypeReference)a.ConstructorArguments[0].Value).Resolve())
            .SelectMany(sm => sm.Methods.Where(m => m.Name == "MoveNext"));

    /// <summary>
    /// The methods a call from <paramref name="body"/> can land in inside Maroik.Core.Service: a Service method called directly, or
    /// every Service implementation of a Contract interface method called through the interface.
    /// </summary>
    private static IEnumerable<MethodDefinition> ServiceCallees(MethodDefinition body) =>
        body.Body.Instructions
            .Where(i => i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt || i.OpCode == OpCodes.Newobj || i.OpCode == OpCodes.Ldftn)
            .Select(i => (MethodReference)i.Operand)
            .SelectMany(callee => callee.DeclaringType.Scope switch
            {
                // A Service method: a module-local reference (from inside Service) or one from Website into the Service assembly.
                ModuleDefinition module when module == _serviceModule => [callee.Resolve()],
                AssemblyNameReference assembly when assembly.Name == _serviceModule.Assembly.Name.Name => [callee.Resolve()],
                // A Contract interface method: wherever Service implements it (no implementation there → nothing to follow).
                AssemblyNameReference assembly when assembly.Name == _contractAssemblyName => Implementations(callee),
                _ => [],
            });

    /// <summary>The <see cref="ServiceErrorKeys"/> values reachable from <paramref name="roots"/> through Maroik.Core.Service.</summary>
    private static HashSet<string> KeysReachableFrom(IEnumerable<MethodDefinition> roots)
    {
        var keys = new HashSet<string>();
        var visited = new HashSet<MethodDefinition>();
        var pending = new Stack<MethodDefinition>(roots.SelectMany(ServiceCallees));
        while (pending.Count > 0)
        {
            MethodDefinition method = pending.Pop();
            if (!visited.Add(method) || !method.HasBody) continue;
            foreach (MethodDefinition stateMachine in StateMachineBody(method)) pending.Push(stateMachine);
            keys.UnionWith(method.Body.Instructions
                .Where(i => i.OpCode == OpCodes.Ldstr && _keys.Contains((string)i.Operand))
                .Select(i => (string)i.Operand));
            foreach (MethodDefinition callee in ServiceCallees(method)) pending.Push(callee);
        }
        return keys;
    }

    /// <summary>Every <see cref="Controller"/> in Maroik.Website, by name.</summary>
    private static IEnumerable<string> ControllerTypeNames() =>
        typeof(AccountController).Assembly.GetTypes()
            .Where(t => typeof(Controller).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => t.Name)
            .Order();

    /// <summary><see cref="ControllerTypeNames"/> as theory data.</summary>
    public static TheoryData<string> ControllerNames() => [.. ControllerTypeNames()];

    /// <summary>The <see cref="ServiceErrorKeys"/> values the controller named <paramref name="controllerName"/> can receive.</summary>
    private static HashSet<string> KeysReachableFromController(string controllerName) =>
        KeysReachableFrom(Flatten(_websiteModule.Types.Single(t => t.Name == controllerName))
            .SelectMany(t => t.Methods)
            .Where(m => m.HasBody));

    /// <summary>The keys of the resx file at <paramref name="path"/> (none when it does not exist).</summary>
    private static HashSet<string> ResxKeys(string path) =>
        File.Exists(path)
            ? XDocument.Load(path).Root!.Elements("data").Select(d => (string)d.Attribute("name")!).ToHashSet()
            : [];

    /// <summary>Every service error key a controller can relay is defined in both cultures of its resx pair.</summary>
    [Theory]
    [MemberData(nameof(ControllerNames))]
    public void EveryServiceErrorKey_AControllerCanReceive_ExistsInItsResxPair(string controllerName)
    {
        var reachable = KeysReachableFromController(controllerName);

        var missing = new List<string>();
        foreach (string culture in new[] { "en-US", "ko-KR" })
        {
            var defined = ResxKeys(Path.Combine(_websiteDir, "Resources", "Controllers", $"{controllerName}.{culture}.resx"));
            missing.AddRange(reachable.Where(k => !defined.Contains(k)).Order().Select(k => $"'{k}' missing in {controllerName}.{culture}.resx"));
        }

        Assert.True(missing.Count == 0, string.Join(Environment.NewLine, missing));
    }

    /// <summary>
    /// Every key is reached from some controller. Keeps the walk above honest — a key it never reaches is either dead or returned
    /// along a path the walk cannot follow, and either way its resx entries would go unchecked.
    /// </summary>
    [Fact]
    public void EveryServiceErrorKey_IsReachedFromSomeController()
    {
        var reached = ControllerTypeNames().SelectMany(KeysReachableFromController).ToHashSet();

        Assert.Empty(_keys.Except(reached).Order());
    }
}
