using System.Text.RegularExpressions;
using Maroik.Core.Contract.Tests.Fixtures;

namespace Maroik.Core.Contract.Tests.ArchitectureRules;

/// <summary>
/// The third-party images in the compose files carry a version: a major (<c>postgres:17</c>), a major.minor
/// (<c>clamav/clamav:1.5</c>) or an exact one (<c>cloudflare/cloudflared:2026.9.3</c>), never <c>latest</c> or no tag at all.
/// A floating tag still brings the patch releases on the next pull, while a release that changes the image's config, ports or
/// behaviour (a new major, or for ClamAV a new minor) arrives only as a deliberate edit. ClamAV matters most: an upload whose
/// scan fails is refused, so an image change that breaks the scanner stops every upload. The debug and production files use
/// the same version of each image, so what is tried locally is what runs. Maroik's own images
/// (<c>ghcr.io/ikhyeonjo/...:${MAROIK_TAG}</c>) are tagged per deployment and left out.
/// </summary>
public class ComposeImageArchitectureTests
{
    /// <summary>The compose files whose images are checked.</summary>
    private static readonly string[] _composeFiles = ["docker-compose.prod.yml", "docker-compose.debug.yml"];

    /// <summary>An <c>image:</c> line, capturing the image name and its tag (empty when there is none).</summary>
    private static readonly Regex _imageLine = new(@"^\s*image:\s*(?<name>[^\s:#]+)(:(?<tag>[^\s#]+))?", RegexOptions.Multiline);

    /// <summary>A tag that names a version: it starts with a number (<c>17</c>, <c>1.5</c>, <c>9-alpine</c>, <c>2026.9.3</c>).</summary>
    private static readonly Regex _versionTag = new(@"^\d");

    [Fact]
    public void EveryThirdPartyImage_CarriesAVersion()
    {
        string[] unversioned =
        [
            .. Images().Where(i => !_versionTag.IsMatch(i.Tag))
                .Select(i => $"{i.File}: {i.Name}:{(i.Tag.Length == 0 ? "(no tag)" : i.Tag)}"),
        ];

        Assert.True(unversioned.Length == 0, string.Join(Environment.NewLine, unversioned));
    }

    [Fact]
    public void TheDebugAndProductionFiles_UseTheSameVersionOfEachImage()
    {
        string[] differing =
        [
            .. Images().GroupBy(i => i.Name, StringComparer.Ordinal)
                .Where(g => g.Select(i => i.Tag).Distinct(StringComparer.Ordinal).Count() > 1)
                .Select(g => $"{g.Key}: " + string.Join(", ", g.Select(i => $"{i.Tag} in {i.File}"))),
        ];

        Assert.True(differing.Length == 0, string.Join(Environment.NewLine, differing));
        Assert.Contains(Images(), i => i.Name == "clamav/clamav");
    }

    /// <summary>The third-party images of every compose file: file, image name and tag.</summary>
    private static IEnumerable<(string File, string Name, string Tag)> Images() =>
        from file in _composeFiles
        from Match match in _imageLine.Matches(File.ReadAllText(Path.Combine(RepositoryPaths.Root, file)))
        let name = match.Groups["name"].Value
        where !name.StartsWith("ghcr.io/ikhyeonjo/", StringComparison.Ordinal)
        select (file, name, match.Groups["tag"].Value);
}
