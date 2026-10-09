using System.Text.RegularExpressions;

namespace Maroik.Core.Service.Tests.Architecture;

/// <summary>
/// The message key of a failed service result is also the key the Website looks up in a controller's resx pair
/// (<c>localizer[result.ErrorKey, result.ErrorArgs]</c>). Written as a string literal at the call site, a reworded message
/// silently stops matching its resx entry and Korean visitors get the English key text back. Every key therefore comes from
/// <c>ServiceErrorKeys</c> (Maroik.Core.Contract), whose values <c>ServiceErrorKeyResxTests</c> (Maroik.Website.Tests) checks
/// against the resx pair of every controller that can surface them.
/// </summary>
/// <remarks>
/// Checked on the source text rather than the IL: a <c>const</c> is inlined as the same <c>ldstr</c> a literal compiles to, so the
/// compiled code cannot tell the two apart. The machine error <em>code</em> (the first argument of
/// <c>ServiceResult.Validation/NotFound/Conflict/Failure</c>, e.g. <c>"Board.NotFound"</c>) is not a resx key and stays a literal.
/// </remarks>
public partial class ServiceErrorKeyArchitectureTests
{
    /// <summary><c>XxxResult.Fail("…"</c> — the key is the first argument.</summary>
    [GeneratedRegex(@"\b\w+Result\.Fail\(\s*""(?<key>(?:[^""\\]|\\.)*)""")]
    private static partial Regex LiteralFailKey();

    /// <summary><c>ServiceResult.Validation/NotFound/Conflict/Failure("code", "…"</c> — the key is the second argument.</summary>
    [GeneratedRegex(@"\bServiceResult\.(?:Validation|NotFound|Conflict|Failure)\(\s*""(?:[^""\\]|\\.)*""\s*,\s*""(?<key>(?:[^""\\]|\\.)*)""")]
    private static partial Regex LiteralCategorizedKey();

    /// <summary>A whole-line <c>//</c> or <c>///</c> comment, which may quote a call as an example.</summary>
    [GeneratedRegex(@"^\s*//.*$", RegexOptions.Multiline)]
    private static partial Regex CommentLine();

    /// <summary>The Maroik.Core.Service project folder (walks up from the test binaries to the folder holding <c>Maroik.sln</c>).</summary>
    private static string ServiceSourceDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Maroik.sln"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Could not locate Maroik.sln"), "Maroik.Core.Service");
    }

    /// <summary>The literal message keys in <paramref name="source"/>, as <c>'key'</c> strings.</summary>
    private static IEnumerable<string> LiteralKeysIn(string source)
    {
        string code = CommentLine().Replace(source, "");
        return LiteralFailKey().Matches(code).Concat(LiteralCategorizedKey().Matches(code))
            .Select(m => $"'{m.Groups["key"].Value}'");
    }

    /// <summary>No service passes a string literal as the message key of a failed result.</summary>
    [Fact]
    public void ServiceResults_TakeTheirMessageKey_FromServiceErrorKeys()
    {
        string serviceDir = ServiceSourceDirectory();
        var sources = Directory.EnumerateFiles(serviceDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Order().ToList();
        Assert.NotEmpty(sources);

        var literals = sources
            .SelectMany(f => LiteralKeysIn(File.ReadAllText(f)).Select(key => $"{Path.GetRelativePath(serviceDir, f)}: {key}"))
            .ToList();

        Assert.True(literals.Count == 0,
            "Use a ServiceErrorKeys constant instead of a literal message key:" + Environment.NewLine +
            string.Join(Environment.NewLine, literals));
    }

    /// <summary>The detector itself: each call shape it must catch, and the shapes it must leave alone.</summary>
    [Theory]
    [InlineData("""return ServiceResult.Fail("Input is invalid");""", "'Input is invalid'")]
    [InlineData("""return LoginResult.Fail("Email or Password is wrong");""", "'Email or Password is wrong'")]
    [InlineData("""return RegisterResult.Fail("'{0}' exists.", errorArgs: [x]);""", "''{0}' exists.'")]
    [InlineData("""return ServiceResult.NotFound("Board.NotFound", "The post could not be found.");""", "'The post could not be found.'")]
    [InlineData("""
                ServiceResult.Validation(
                    "Expenditure.SameAsset", "The values cannot be the same.");
                """, "'The values cannot be the same.'")]
    [InlineData("""return ServiceResult.Fail(ServiceErrorKeys.InputInvalid);""", null)]
    [InlineData("""return ServiceResult.NotFound("Board.NotFound", ServiceErrorKeys.PostNotFound);""", null)]
    [InlineData("""/// <c>ServiceResult.Fail("example")</c>""", null)]
    public void LiteralKeysIn_FindsExactlyTheLiteralKeys(string source, string? expected)
    {
        Assert.Equal(expected is null ? [] : [expected], LiteralKeysIn(source));
    }
}
