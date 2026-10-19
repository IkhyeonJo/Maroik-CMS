using System.Reflection;
using System.Reflection.Emit;
using Maroik.Website.Constants;

namespace Maroik.Website.Tests;

/// <summary>Unit tests for <see cref="AppVersion"/>.</summary>
public class AppVersionTests
{
    /// <summary>A throwaway in-memory assembly, carrying <paramref name="informationalVersion"/> when it is not null.</summary>
    private static AssemblyBuilder DynamicAssembly(string? informationalVersion)
    {
        var builder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("AppVersionTests.Dynamic"), AssemblyBuilderAccess.Run);
        if (informationalVersion is not null)
        {
            var constructor = typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)]);
            Assert.NotNull(constructor);
            builder.SetCustomAttribute(new CustomAttributeBuilder(constructor, [informationalVersion]));
        }
        return builder;
    }

    /// <summary>
    /// <see cref="AppVersion.Informational"/> is the website assembly's InformationalVersion exactly as the build stamped it
    /// (with the "-dev" suffix and the "+commit" part), not the four-part AssemblyVersion, which drops both.
    /// </summary>
    [Fact]
    public void Informational_IsTheWebsiteAssemblysInformationalVersion()
    {
        var attribute = typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(attribute.InformationalVersion, AppVersion.Informational);
    }

    /// <summary>A build without CI's version arguments (local, docker-compose.debug.yml, the test run) is marked as a development build.</summary>
    [Fact]
    public void Informational_OfALocalBuild_IsMarkedAsADevelopmentBuild() =>
        Assert.StartsWith("1.0.0-dev", AppVersion.Informational, StringComparison.Ordinal);

    /// <summary><see cref="AppVersion.Read"/> returns the attribute's value unchanged, "+commit" part included.</summary>
    [Fact]
    public void Read_ReturnsTheInformationalVersionUnchanged() =>
        Assert.Equal("1.4.2+2ecebf469478", AppVersion.Read(DynamicAssembly("1.4.2+2ecebf469478")));

    /// <summary>An assembly without an InformationalVersion reads as "unknown".</summary>
    [Fact]
    public void Read_ReturnsUnknown_WhenTheAssemblyHasNoInformationalVersion() =>
        Assert.Equal("unknown", AppVersion.Read(DynamicAssembly(null)));

    /// <summary>The footer text is "v" and the part before the first '+': the build metadata (the commit) is left out.</summary>
    [Theory]
    [InlineData("1.4.2+2ecebf469478", "v1.4.2")]
    [InlineData("1.0.0-dev+0123456789abcdef", "v1.0.0-dev")]
    [InlineData("1.0.0-dev", "v1.0.0-dev")]
    public void ToDisplay_KeepsThePartBeforeThePlus(string informational, string expected) =>
        Assert.Equal(expected, AppVersion.ToDisplay(informational));

    /// <summary><see cref="AppVersion.Display"/> is <see cref="AppVersion.Informational"/> in its footer form.</summary>
    [Fact]
    public void Display_IsTheInformationalVersionInItsFooterForm() =>
        Assert.Equal(AppVersion.ToDisplay(AppVersion.Informational), AppVersion.Display);
}
