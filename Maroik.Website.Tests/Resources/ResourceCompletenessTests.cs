using System.Text.RegularExpressions;
using System.Xml.Linq;
using Maroik.Website.Tests.Fixtures;

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
    private static readonly string _websiteDir = RepositoryPaths.Website;
    /// <summary>The website's <c>Resources</c> folder.</summary>
    private static readonly string _resourcesDir = Path.Combine(_websiteDir, "Resources");

    /// <summary>Values that are legitimately empty in one culture (English has no year suffix in a date picker).</summary>
    private static readonly HashSet<(string Culture, string Key)> _allowedEmpty = [("en-US", "YearSuffix")];

    /// <summary>Every <c>data</c> entry of the resx file at <paramref name="path"/>, name to value (missing values as empty).</summary>
    private static Dictionary<string, string> ReadResx(string path) =>
        XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? "");

    /// <summary>Every resx file under <see cref="_resourcesDir"/>, sorted.</summary>
    private static IEnumerable<string> ResxFiles() =>
        Directory.EnumerateFiles(_resourcesDir, "*.resx", SearchOption.AllDirectories).Order();

    /// <summary>The path of <paramref name="resxPath"/> relative to <see cref="_resourcesDir"/>, without its culture suffix and extension.</summary>
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
            let cultures = @group.Select(f => Regex.Match(f, @"\.(en-US|ko-KR)\.resx$").Groups[1].Value).Order().ToList()
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
            string culture = Regex.Match(file, @"\.(en-US|ko-KR)\.resx$").Groups[1].Value;
            foreach (var (key, value) in ReadResx(file))
                if (string.IsNullOrWhiteSpace(value) && !_allowedEmpty.Contains((culture, key))) blank.Add($"{Stem(file)} [{culture}]: '{key}'");
        }

        Assert.True(blank.Count == 0, string.Join(Environment.NewLine, blank));
    }

    // A literal key asked of a localizer: Localizer["..."], _localizer["..."], localizer["..."] (C# string escapes for \" only).
    private static readonly Regex _literalKey = new("""
                                                    \b_?[Ll]ocalizer\s*\[\s*"((?:[^"\\]|\\.)*)"
                                                    """, RegexOptions.Compiled);

    /// <summary>The distinct literal localizer keys in the file at <paramref name="path"/>, unescaped.</summary>
    private static IEnumerable<string> LiteralKeys(string path) =>
        _literalKey.Matches(File.ReadAllText(path)).Select(m => m.Groups[1].Value.Replace("\\\"", "\"", StringComparison.Ordinal).Replace(@"\\", "\\", StringComparison.Ordinal)).Distinct();

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

    // `Localizer[nameof(Type.Member)]`: a grid column title, looked up under the member's name.
    private static readonly Regex _nameofKey = new(@"\b_?[Ll]ocalizer\s*\[\s*nameof\((?:[\w.]*\.)?(\w+)\)\s*\]", RegexOptions.Compiled);

    // A lookup whose key is neither a literal nor a nameof: an expression (a taxonomy item, a menu name).
    private static readonly Regex _dynamicKey = new(@"\b_?[Ll]ocalizer\s*\[\s*(?!""|nameof\()", RegexOptions.Compiled);

    /// <summary>
    /// The system texts an account's <c>Message</c> can hold, which the account grid translates (<c>Localizer[model.Message]</c>):
    /// every <see cref="Maroik.Core.Domain.Accounts.AccountMessage"/> description and the lock text <c>Account</c> writes itself.
    /// An administrator's own free text is not among them and is shown as typed.
    /// </summary>
    private static HashSet<string> StoredAccountMessages()
    {
        var messages = typeof(Maroik.Core.Domain.Accounts.AccountMessage).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Select(field => field.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
                .OfType<System.ComponentModel.DescriptionAttribute>().Single().Description)
            .ToHashSet(StringComparer.Ordinal);
        var lockMessage = typeof(Maroik.Core.Domain.Accounts.Account).GetField("AccountLockedMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(lockMessage);
        messages.Add(Assert.IsType<string>(lockMessage.GetRawConstantValue()));
        return messages;
    }

    /// <summary>
    /// Every system text an account's <c>Message</c> can hold has its key in the account grid's resx pair, so an administrator
    /// reading the grid in Korean is not shown the English text.
    /// </summary>
    [Fact]
    public void EveryStoredAccountMessage_IsTranslatedInTheAccountGrid()
    {
        var missing = new List<string>();
        foreach (string culture in new[] { "en-US", "ko-KR" })
        {
            var defined = ReadResx(Path.Combine(_resourcesDir, "Views", "Management", $"_AccountGrid.{culture}.resx"));
            missing.AddRange(StoredAccountMessages().Where(m => !defined.ContainsKey(m)).Select(m => $"'{m}' missing in _AccountGrid.{culture}.resx"));
        }

        Assert.True(missing.Count == 0, string.Join(Environment.NewLine, missing));
    }

    /// <summary>
    /// The keys a view may look up through an expression: the names of the domain's enums (the finance taxonomies the forms
    /// list, e.g. <c>AssetItemType</c>), the display names of the seeded menu (the side bar and the page header) and the system
    /// texts an account's message can hold (the account grid, <see cref="StoredAccountMessages"/>).
    /// </summary>
    private static HashSet<string> DynamicViewKeys()
    {
        var keys = typeof(Maroik.Core.Domain.Accounts.PasswordPolicy).Assembly.GetTypes()
            .Where(type => type.IsEnum)
            .SelectMany(Enum.GetNames)
            .ToHashSet(StringComparer.Ordinal);
        keys.UnionWith(StoredAccountMessages());

        string seed = File.ReadAllText(Path.Combine(RepositoryPaths.Root, "Maroik.DB", "PostgreSQL", "SQL_Init_Script", "Debugging", "Init.sql"));
        foreach (var (table, displayNameColumn) in new[] { ("Category", 2), ("SubCategory", 3) })
        {
            Match rows = Regex.Match(seed, $"COPY public\\.\"{table}\" .*?FROM stdin;\\n(.*?)\\n\\\\\\.", RegexOptions.Singleline);
            Assert.True(rows.Success, $"no {table} rows in the seed");
            keys.UnionWith(rows.Groups[1].Value.Split('\n').Select(row => row.Split('\t')[displayNameColumn]));
        }

        return keys;
    }

    /// <summary>
    /// Every key of a view's resx pair is asked for by that view: as a literal, as a grid column's <c>nameof</c>, or — in a view
    /// that looks keys up through an expression — as a taxonomy or menu name (<see cref="DynamicViewKeys"/>). A key nothing asks for
    /// is left over from a removed or copied piece of markup.
    /// </summary>
    [Fact]
    public void EveryViewResxKey_IsAskedForByItsView()
    {
        string viewsDir = Path.Combine(_websiteDir, "Views");
        string viewResources = Path.Combine(_resourcesDir, "Views");
        HashSet<string> dynamicKeys = DynamicViewKeys();
        var resxFiles = Directory.EnumerateFiles(viewResources, "*.en-US.resx", SearchOption.AllDirectories).Order().ToList();
        Assert.NotEmpty(resxFiles);

        var unused = new List<string>();
        foreach (string resx in resxFiles)
        {
            string stem = Regex.Replace(Path.GetRelativePath(viewResources, resx).Replace('\\', '/'), @"\.en-US\.resx$", "");
            string view = Path.Combine(viewsDir, stem + ".cshtml");
            if (!File.Exists(view)) { unused.Add($"Views/{stem}: no view for this resx pair"); continue; }

            string source = File.ReadAllText(view);
            var asked = LiteralKeys(view).Concat(_nameofKey.Matches(source).Select(m => m.Groups[1].Value)).ToHashSet(StringComparer.Ordinal);
            bool dynamic = _dynamicKey.IsMatch(source);
            unused.AddRange(ReadResx(resx).Keys
                .Where(key => !asked.Contains(key) && !(dynamic && dynamicKeys.Contains(key)))
                .Select(key => $"Views/{stem}: '{key}' is asked for by nothing"));
        }

        Assert.True(unused.Count == 0, string.Join(Environment.NewLine, unused));
    }

    // A C# string literal ("..." with \-escapes; the verbatim and raw forms are not used for localized text).
    private static readonly Regex _stringLiteral = new("""
                                                       "((?:[^"\\\n]|\\.)*)"
                                                       """, RegexOptions.Compiled);

    // `nameof(Type.Member)` anywhere in the code: a key passed on as a member name (an Excel column title).
    private static readonly Regex _nameof = new(@"\bnameof\((?:[\w.]*\.)?(\w+)\)", RegexOptions.Compiled);

    /// <summary>
    /// Every key of a controller's resx pair is referenced by the production code. A controller also looks keys up through what a
    /// service or the domain hands it (a <c>ServiceErrorKeys</c> constant, a <c>DomainError</c> template, a taxonomy name), so a key
    /// counts as referenced when it is a string literal, a <c>nameof</c> name or a constant's value anywhere in <c>Maroik.Website</c> /
    /// <c>Maroik.Core.*</c>, or an enum member's name. A coarser check than the views' (a key need only appear somewhere), it still
    /// catches the messages left behind when a rule's wording moved into the domain.
    /// </summary>
    [Fact]
    public void EveryControllerResxKey_IsReferencedByTheProductionCode()
    {
        var referenced = new HashSet<string>(StringComparer.Ordinal);
        var sources = Directory.EnumerateDirectories(RepositoryPaths.Root)
            .Where(dir => Path.GetFileName(dir) is var name && (name == "Maroik.Website" || name.StartsWith("Maroik.Core.", StringComparison.Ordinal)) && !name.EndsWith(".Tests", StringComparison.Ordinal))
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();
        Assert.NotEmpty(sources);
        foreach (string file in sources)
        {
            string text = File.ReadAllText(file);
            referenced.UnionWith(_stringLiteral.Matches(text).Select(m => m.Groups[1].Value.Replace("\\\"", "\"", StringComparison.Ordinal).Replace(@"\\", "\\", StringComparison.Ordinal)));
            referenced.UnionWith(_nameof.Matches(text).Select(m => m.Groups[1].Value));
        }

        // Constants built by concatenation (PasswordPolicy.ViolationMessage) and enum member names, read from the assemblies.
        Type[] types =
        [
            .. new[]
            {
                typeof(Maroik.Core.Domain.Accounts.PasswordPolicy).Assembly,
                typeof(Maroik.Core.Contract.Dtos.ServiceErrorKeys).Assembly,
                typeof(Maroik.Website.Models.ViewModels.Accounts.LoginInputViewModel).Assembly,
            }.SelectMany(assembly => assembly.GetTypes())
        ];
        referenced.UnionWith(types.SelectMany(type => type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static))
            .Where(field => field is { IsLiteral: true } && field.FieldType == typeof(string))
            .Select(field => (string?)field.GetRawConstantValue())
            .OfType<string>());
        referenced.UnionWith(types.Where(type => type.IsEnum).SelectMany(Enum.GetNames));

        var resxFiles = Directory.EnumerateFiles(Path.Combine(_resourcesDir, "Controllers"), "*.en-US.resx").Order().ToList();
        Assert.NotEmpty(resxFiles);
        var unused = resxFiles
            .SelectMany(resx => ReadResx(resx).Keys
                .Where(key => !referenced.Contains(key))
                .Select(key => $"Controllers/{Path.GetFileName(resx)}: '{key}' is referenced by nothing"))
            .ToList();

        Assert.True(unused.Count == 0, string.Join(Environment.NewLine, unused));
    }

    // A domain error built through DomainError: its code, then its English message template (the resx key the controller looks up).
    private static readonly Regex _domainError = new("""
                                                     DomainError\.(?:Validation|Conflict|Failure)\(\s*\$?"[^"]*"\s*,\s*"((?:[^"\\]|\\.)*)"
                                                     """, RegexOptions.Compiled);

    /// <summary>
    /// Every message template a <c>DomainError</c> is built with is a key of at least one controller's resx pair, in both cultures —
    /// the controllers translate a domain refusal by its template, so a template with no entry reaches Korean visitors in English.
    /// </summary>
    [Fact]
    public void EveryDomainErrorTemplate_IsTranslatedByAController()
    {
        var templates = Directory.EnumerateFiles(Path.Combine(RepositoryPaths.Root, "Maroik.Core.Domain"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(file => _domainError.Matches(File.ReadAllText(file)).Select(m => m.Groups[1].Value.Replace("\\\"", "\"", StringComparison.Ordinal)))
            .ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(templates);

        var missing = new List<string>();
        foreach (string culture in new[] { "en-US", "ko-KR" })
        {
            var defined = Directory.EnumerateFiles(Path.Combine(_resourcesDir, "Controllers"), $"*.{culture}.resx")
                .SelectMany(resx => ReadResx(resx).Keys).ToHashSet(StringComparer.Ordinal);
            missing.AddRange(templates.Where(t => !defined.Contains(t)).Order(StringComparer.Ordinal).Select(t => $"'{t}' is in no controller's {culture} resx"));
        }

        Assert.True(missing.Count == 0, string.Join(Environment.NewLine, missing));
    }

    /// <summary>
    /// Every key of a view model's resx pair is used by that view model: as a validation attribute's <c>ErrorMessage</c> or a
    /// <c>Display</c> name (the two texts <c>AddDataAnnotationsLocalization</c> looks up there).
    /// </summary>
    [Fact]
    public void EveryViewModelResxKey_IsUsedByItsViewModel()
    {
        string modelResources = Path.Combine(_resourcesDir, "Models");
        var assembly = typeof(Maroik.Website.Models.ViewModels.Accounts.LoginInputViewModel).Assembly;
        var resxFiles = Directory.EnumerateFiles(modelResources, "*.en-US.resx", SearchOption.AllDirectories).Order().ToList();
        Assert.NotEmpty(resxFiles);

        var unused = new List<string>();
        foreach (string resx in resxFiles)
        {
            string stem = Regex.Replace(Path.GetRelativePath(modelResources, resx).Replace('\\', '/'), @"\.en-US\.resx$", "");
            Type? viewModel = assembly.GetType("Maroik.Website.Models." + stem.Replace('/', '.'));
            if (viewModel == null) { unused.Add($"Models/{stem}: no view model for this resx pair"); continue; }

            var properties = viewModel.GetProperties();
            var used = properties
                .SelectMany(p => p.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.ValidationAttribute), true)
                    .OfType<System.ComponentModel.DataAnnotations.ValidationAttribute>().Select(a => a.ErrorMessage))
                .Concat(properties.Select(p => p.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.DisplayAttribute), true)
                    .OfType<System.ComponentModel.DataAnnotations.DisplayAttribute>().SingleOrDefault()?.Name))
                .OfType<string>()
                .ToHashSet(StringComparer.Ordinal);
            unused.AddRange(ReadResx(resx).Keys.Where(key => !used.Contains(key)).Select(key => $"Models/{stem}: '{key}' is used by nothing"));
        }

        Assert.True(unused.Count == 0, string.Join(Environment.NewLine, unused));
    }

    /// <summary>
    /// Every <c>ErrorMessage</c> of a view model's validation attribute is defined in <c>Resources/Models/ViewModels/…/{Type}</c> — the
    /// resx pair <c>AddDataAnnotationsLocalization</c> looks it up in. A message whose text changes (a placeholder in place of a
    /// number) must have its key renamed in both cultures.
    /// </summary>
    [Fact]
    public void EveryValidationMessage_OfAViewModel_ExistsInItsResxPair()
    {
        var viewModels = typeof(Maroik.Website.Models.ViewModels.Accounts.LoginInputViewModel).Assembly.GetTypes()
            .Where(type => type.Namespace?.StartsWith("Maroik.Website.Models.ViewModels", StringComparison.Ordinal) == true && !type.IsNested)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();
        Assert.NotEmpty(viewModels);

        var missing = new List<string>();
        foreach (Type viewModel in viewModels)
        {
            var messages = viewModel.GetProperties()
                .SelectMany(property => property.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.ValidationAttribute), true)
                    .OfType<System.ComponentModel.DataAnnotations.ValidationAttribute>())
                .Select(attribute => attribute.ErrorMessage)
                .OfType<string>()
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (messages.Count == 0) continue;

            string stem = viewModel.FullName!["Maroik.Website.".Length..].Replace('.', '/');
            foreach (string culture in new[] { "en-US", "ko-KR" })
            {
                string resx = Path.Combine(_resourcesDir, $"{stem}.{culture}.resx");
                if (!File.Exists(resx)) { missing.Add($"{viewModel.Name}: no {stem}.{culture}.resx for its {messages.Count} message(s)"); continue; }
                var defined = ReadResx(resx);
                missing.AddRange(messages.Where(m => !defined.ContainsKey(m)).Select(m => $"{viewModel.Name}: '{m}' missing in {stem}.{culture}.resx"));
            }
        }

        Assert.True(missing.Count == 0, string.Join(Environment.NewLine, missing));
    }
}
