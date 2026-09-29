using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Maroik.Website.Tests.Resources;

/// <summary>
/// The localization contract (CLAUDE.md): every user-facing string goes through the resx pair of its class, both cultures filled in.
/// Checked against the files themselves, without starting the site: each resx has both cultures with identical keys and no empty
/// value, and every literal key a controller or a view asks its localizer for exists in that class's resx pair — a missing key would
/// silently fall back to the (English) key text for Korean visitors.
/// </summary>
public class ResourceCompletenessTests
{
    /// <summary>The Maroik.Website project folder.</summary>
    private static readonly string _websiteDir = FindWebsiteDirectory();
    /// <summary>The website's <c>Resources</c> folder.</summary>
    private static readonly string _resourcesDir = Path.Combine(_websiteDir, "Resources");

    /// <summary>Values that are legitimately empty in one culture (English has no year suffix in a date picker).</summary>
    private static readonly HashSet<(string Culture, string Key)> _allowedEmpty = [("en-US", "YearSuffix")];

    /// <summary>Walks up to the directory holding <c>Maroik.sln</c> and returns its <c>Maroik.Website</c> subfolder.</summary>
    private static string FindWebsiteDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Maroik.sln"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Could not locate Maroik.sln"), "Maroik.Website");
    }

    /// <summary>Every <c>data</c> entry of the resx file at <paramref name="path"/>, name to value (missing values as empty).</summary>
    private static Dictionary<string, string> ReadResx(string path) =>
        XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? "");

    /// <summary>Every resx file under <see cref="_resourcesDir"/>, sorted.</summary>
    private static IEnumerable<string> ResxFiles() =>
        Directory.EnumerateFiles(_resourcesDir, "*.resx", SearchOption.AllDirectories).Order();

    /// <summary>The path of <paramref name="resxPath"/> relative to <see cref="_resourcesDir"/>, without its culture suffix and extension.</summary>
    [SuppressMessage("Performance", "SYSLIB1045:\'GeneratedRegexAttribute\'로 변환합니다.")]
    private static string Stem(string resxPath)
    {
        string relative = Path.GetRelativePath(_resourcesDir, resxPath).Replace('\\', '/');
        return Regex.Replace(relative, @"\.(en-US|ko-KR)\.resx$", "");
    }

    /// <summary>Every resx file is one half of an en-US / ko-KR pair.</summary>
    [Fact]
    public void EveryResx_HasBothCultures()
    {
        var byStem = ResxFiles().GroupBy(Stem).ToList();
        var problems = (from @group in byStem
 #pragma warning disable SYSLIB1045
            let cultures = @group.Select(f => Regex.Match(f, @"\.(en-US|ko-KR)\.resx$").Groups[1].Value).Order().ToList()
 #pragma warning restore SYSLIB1045
            where !cultures.SequenceEqual([
                "en-US", "ko-KR"
            ])
            select $"{@group.Key}: {string.Join(",", cultures)}").ToList();

        Assert.NotEmpty(byStem);
        Assert.True(problems.Count == 0, "resx files that are not an en-US + ko-KR pair: " + string.Join("; ", problems));
    }

    /// <summary>The two cultures of a pair define exactly the same keys.</summary>
    [Fact]
    public void EveryResxPair_DefinesTheSameKeysInBothCultures()
    {
        var problems = new List<string>();
        foreach (var group in ResxFiles().GroupBy(Stem))
        {
            var en = group.Single(f => f.EndsWith(".en-US.resx", StringComparison.Ordinal));
            var ko = group.Single(f => f.EndsWith(".ko-KR.resx", StringComparison.Ordinal));
            var enKeys = ReadResx(en).Keys.ToHashSet();
            var koKeys = ReadResx(ko).Keys.ToHashSet();
            problems.AddRange(enKeys.Except(koKeys).Select(key => $"{group.Key}: '{key}' only in en-US"));
            problems.AddRange(koKeys.Except(enKeys).Select(key => $"{group.Key}: '{key}' only in ko-KR"));
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    /// <summary>No entry is blank (apart from the few that are legitimately empty in one culture).</summary>
    [Fact]
    public void NoResxValue_IsBlank()
    {
        var blank = new List<string>();
        foreach (string file in ResxFiles())
        {
 #pragma warning disable SYSLIB1045
            string culture = Regex.Match(file, @"\.(en-US|ko-KR)\.resx$").Groups[1].Value;
 #pragma warning restore SYSLIB1045
            foreach (var (key, value) in ReadResx(file))
                if (string.IsNullOrWhiteSpace(value) && !_allowedEmpty.Contains((culture, key))) blank.Add($"{Stem(file)} [{culture}]: '{key}'");
        }

        Assert.True(blank.Count == 0, string.Join(Environment.NewLine, blank));
    }

    // A literal key asked of a localizer: Localizer["..."], _localizer["..."], localizer["..."] (C# string escapes for \" only).
 #pragma warning disable SYSLIB1045
    private static readonly Regex _literalKey = new("""
                                                    \b_?[Ll]ocalizer\s*\[\s*"((?:[^"\\]|\\.)*)"
                                                    """, RegexOptions.Compiled);
 #pragma warning restore SYSLIB1045

    /// <summary>The distinct literal localizer keys in the file at <paramref name="path"/>, unescaped.</summary>
    private static IEnumerable<string> LiteralKeys(string path) =>
        _literalKey.Matches(File.ReadAllText(path)).Select(m => m.Groups[1].Value.Replace("\\\"", "\"").Replace(@"\\", "\\")).Distinct();

    /// <summary>The literal keys in <paramref name="sourceFiles"/> absent from their resx pair (located by <paramref name="resxStemFor"/>), as readable failure lines.</summary>
    private static List<string> MissingKeys(IEnumerable<string> sourceFiles, Func<string, string> resxStemFor)
    {
        var missing = new List<string>();
        foreach (string source in sourceFiles)
        {
            var keys = LiteralKeys(source).ToList();
            if (keys.Count == 0) continue;
            string stem = resxStemFor(source);
            foreach (string culture in new[] { "en-US", "ko-KR" })
            {
                string resx = Path.Combine(_resourcesDir, $"{stem}.{culture}.resx");
                if (!File.Exists(resx)) { missing.Add($"{Path.GetRelativePath(_websiteDir, source)}: no {stem}.{culture}.resx for its {keys.Count} key(s)"); continue; }
                var defined = ReadResx(resx);
                missing.AddRange(keys.Where(k => !defined.ContainsKey(k)).Select(key => $"{Path.GetRelativePath(_websiteDir, source)}: '{key}' missing in {stem}.{culture}.resx"));
            }
        }
        return missing;
    }

    /// <summary>Every literal key a controller asks its localizer for is defined in <c>Resources/Controllers/{Controller}</c>.</summary>
    [Fact]
    public void EveryLiteralKey_UsedByAController_ExistsInItsResxPair()
    {
        var controllers = Directory.EnumerateFiles(Path.Combine(_websiteDir, "Controllers"), "*Controller.cs").Order().ToList();
        Assert.NotEmpty(controllers);

        var missing = MissingKeys(controllers, f => "Controllers/" + Path.GetFileNameWithoutExtension(f));

        Assert.True(missing.Count == 0, string.Join(Environment.NewLine, missing));
    }

    /// <summary>Every literal key a view asks its localizer for is defined in <c>Resources/Views/{path of the view}</c>.</summary>
    [Fact]
    public void EveryLiteralKey_UsedByAView_ExistsInItsResxPair()
    {
        string viewsDir = Path.Combine(_websiteDir, "Views");
        // (_ViewImports / _ViewStart only configure the views; a `Localizer["key"]` in them is documentation, not a lookup)
        var views = Directory.EnumerateFiles(viewsDir, "*.cshtml", SearchOption.AllDirectories)
            .Where(f => Path.GetFileName(f) is not ("_ViewImports.cshtml" or "_ViewStart.cshtml")).Order().ToList();
        Assert.NotEmpty(views);

        var missing = MissingKeys(views, f => "Views/" + Path.ChangeExtension(Path.GetRelativePath(viewsDir, f), null).Replace('\\', '/'));

        Assert.True(missing.Count == 0, string.Join(Environment.NewLine, missing));
    }
}
