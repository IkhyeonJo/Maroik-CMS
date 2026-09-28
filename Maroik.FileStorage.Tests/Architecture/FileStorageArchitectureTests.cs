using System.Reflection;
using System.Runtime.CompilerServices;
using Maroik.FileStorage.Controllers;
using Microsoft.AspNetCore.Mvc;
using NetArchTest.Rules;

namespace Maroik.FileStorage.Tests.Architecture;

/// <summary>
/// Architecture tests enforcing that Maroik.FileStorage stays a standalone microservice, reached
/// only over its HTTP API, rather than acquiring a direct dependency on the monolith's internal
/// layers.
/// </summary>
public class FileStorageArchitectureTests
{
    private static readonly Assembly _fileStorageAssembly = typeof(FileController).Assembly;

    /// <summary>FileStorage should not depend on the monolith's internal layers.</summary>
    [Theory]
    [InlineData("Maroik.Core.Service")]
    [InlineData("Maroik.Core.Repository")]
    [InlineData("Maroik.Core.PostgreSQL")]
    [InlineData("Maroik.Website")]
    public void FileStorage_ShouldNot_DependOnMonolithInternalLayers(string forbiddenNamespace)
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(_fileStorageAssembly)
            .ShouldNot()
            .HaveDependencyOn(forbiddenNamespace)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"Maroik.FileStorage must not reference {forbiddenNamespace}. " +
            "FileStorage is a standalone microservice reached only over its HTTP API " +
            "(see Maroik.Core.Client.Clients.FileClient) and must not pull in the monolith's " +
            "internal layers directly.\n" +
            "Failing types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// Controllers must not declare non-public helper methods, mirroring the same rule for
    /// Maroik.Website (see WebsiteArchitectureTests.Controllers_ShouldNot_DeclareNonPublicMethods):
    /// every method the routing system can dispatch to should be a public action, and shared logic
    /// should live in a service instead of a controller-local helper.
    /// </summary>
    [Fact]
    public void Controllers_ShouldNot_DeclareNonPublicMethods()
    {
        List<string> violations =
        [
            .. from controllerType in _fileStorageAssembly.GetTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ControllerBase).IsAssignableFrom(t))
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
            "service instead.\n" +
            "Violations: " + string.Join("; ", violations));
    }

    /// <summary>
    /// The <c>Services</c> namespace/folder is for DI-registered service classes only — each a
    /// non-static class that implements an interface from <c>Maroik.FileStorage.Contracts</c>. Pure
    /// static helpers (e.g. <c>StoragePath</c>) belong under <c>Helpers/</c>, not here: a static
    /// utility class in <c>Services/</c> blurs the "everything in here is an injected service"
    /// invariant, and is exactly how <c>StoragePath</c> was first misfiled.
    /// </summary>
    [Fact]
    public void ServicesNamespace_ContainsOnly_DiServiceClasses()
    {
        var contractInterfaces = _fileStorageAssembly.GetTypes()
            .Where(t => t is { IsInterface: true, Namespace: "Maroik.FileStorage.Contracts" })
            .ToHashSet();

        List<string> violations = [];

        foreach (Type type in _fileStorageAssembly.GetTypes()
                     .Where(t => t is { Namespace: "Maroik.FileStorage.Services", IsNested: false }))
        {
            if (type.GetCustomAttribute<CompilerGeneratedAttribute>() != null)
                continue;

            // A C# "static class" compiles to abstract + sealed.
            if (type is { IsAbstract: true, IsSealed: true })
            {
                violations.Add($"{type.Name}: static helper class — move it to Maroik.FileStorage/Helpers");
                continue;
            }

            if (!type.GetInterfaces().Any(contractInterfaces.Contains))
                violations.Add($"{type.Name}: implements no Maroik.FileStorage.Contracts interface");
        }

        Assert.True(
            violations.Count == 0,
            "Types under Maroik.FileStorage.Services must be DI-registered service classes that " +
            "implement a Maroik.FileStorage.Contracts interface. Static helpers belong in " +
            "Maroik.FileStorage/Helpers.\nViolations:\n  " + string.Join("\n  ", violations));
    }
}
