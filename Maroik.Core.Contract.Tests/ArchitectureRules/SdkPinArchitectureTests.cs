using System.Text.Json;
using System.Text.RegularExpressions;
using Maroik.Core.Contract.Tests.Fixtures;

namespace Maroik.Core.Contract.Tests.ArchitectureRules;

/// <summary>
/// One .NET SDK builds the solution everywhere: on a dev box (<c>global.json</c>), in CI (<c>setup-dotnet</c> reads the same
/// file) and in every Dockerfile's build stage. The analyzers run as build errors, and an SDK update can bring a newer
/// Roslyn with new diagnostics (or an analyzer that no longer loads), so a floating SDK could break a build that did not
/// change. Bumping the SDK is therefore one deliberate change to all of these places at once.
/// The runtime images (<c>aspnet</c> / <c>runtime</c>) stay on the floating <c>10.0</c> tag on purpose: they only run the
/// published app and should pick up runtime security patches without a commit.
/// </summary>
public class SdkPinArchitectureTests
{
    /// <summary>The <c>FROM</c> line of a Dockerfile build stage on the .NET SDK image, capturing its tag.</summary>
    private static readonly Regex _sdkImage = new(@"^FROM\s+mcr\.microsoft\.com/dotnet/sdk:(?<tag>\S+)", RegexOptions.Multiline);

    /// <summary>The SDK version and roll-forward policy <c>global.json</c> pins.</summary>
    private static (string? Version, string? RollForward) ReadGlobalJson()
    {
        using JsonDocument json = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryPaths.Root, "global.json")));
        if (!json.RootElement.TryGetProperty("sdk", out JsonElement sdk)) return (null, null);
        return (sdk.TryGetProperty("version", out JsonElement v) ? v.GetString() : null,
                sdk.TryGetProperty("rollForward", out JsonElement r) ? r.GetString() : null);
    }

    [Fact]
    public void GlobalJson_PinsAnExactSdk_WithoutRollingForward()
    {
        (string? version, string? rollForward) = ReadGlobalJson();

        Assert.NotNull(version);
        Assert.Matches(@"^\d+\.\d+\.\d{3}$", version);
        Assert.Equal("disable", rollForward);
    }

    [Fact]
    public void EveryDockerfile_BuildsWithTheSdkGlobalJsonPins()
    {
        string? version = ReadGlobalJson().Version;
        string[] dockerfiles = Directory.GetFiles(RepositoryPaths.Root, "Dockerfile", SearchOption.AllDirectories)
            .Where(f => !RepositoryPaths.HasSegment(f, "bin") && !RepositoryPaths.HasSegment(f, "obj"))
            .ToArray();

        Assert.NotEmpty(dockerfiles);
        List<string> violations = [];
        foreach (string dockerfile in dockerfiles)
        {
            string[] tags = [.. _sdkImage.Matches(File.ReadAllText(dockerfile)).Select(m => m.Groups["tag"].Value)];
            string name = Path.GetRelativePath(RepositoryPaths.Root, dockerfile);
            if (tags.Length == 0) violations.Add($"{name}: no build stage on mcr.microsoft.com/dotnet/sdk");
            violations.AddRange(tags.Where(t => !string.Equals(t, version, StringComparison.Ordinal))
                .Select(t => $"{name}: sdk:{t}, global.json pins {version}"));
        }

        Assert.Empty(violations);
    }

    /// <summary>
    /// The weekly bump (<c>.github/workflows/sdk-bump.yml</c>) rewrites global.json and every <c>&lt;project&gt;/Dockerfile</c>
    /// in one pull request, so it never opens a bump the test above would fail. It finds the Dockerfiles with that one-level
    /// glob, so every Dockerfile must sit directly in a project folder.
    /// </summary>
    [Fact]
    public void TheSdkBumpWorkflow_RewritesEveryPinnedFile()
    {
        string workflow = File.ReadAllText(Path.Combine(RepositoryPaths.Root, ".github", "workflows", "sdk-bump.yml"));
        string[] nested =
        [
            .. Directory.GetFiles(RepositoryPaths.Root, "Dockerfile", SearchOption.AllDirectories)
                .Where(f => !RepositoryPaths.HasSegment(f, "bin") && !RepositoryPaths.HasSegment(f, "obj"))
                .Select(f => Path.GetRelativePath(RepositoryPaths.Root, f))
                .Where(f => f.Split(Path.DirectorySeparatorChar).Length != 2),
        ];

        Assert.Contains("global.json", workflow, StringComparison.Ordinal);
        Assert.Contains("*/Dockerfile", workflow, StringComparison.Ordinal);
        Assert.Contains("SdkPinArchitectureTests", workflow, StringComparison.Ordinal);
        Assert.Empty(nested);
    }

    [Fact]
    public void Ci_InstallsTheSdkFromGlobalJson()
    {
        string workflow = File.ReadAllText(Path.Combine(RepositoryPaths.Root, ".github", "workflows", "ci-cd.yml"));

        Assert.Contains("global-json-file: global.json", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet-version:", workflow, StringComparison.Ordinal);
    }
}
