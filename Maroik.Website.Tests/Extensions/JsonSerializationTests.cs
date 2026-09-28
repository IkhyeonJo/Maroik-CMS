using Maroik.Website.Extensions;
// ReSharper disable NotAccessedPositionalProperty.Local

namespace Maroik.Website.Tests.Extensions;

/// <summary>
/// Unit tests for <see cref="JsonSerialization.ToClientJson"/> -- the shared
/// <c>System.Text.Json</c> settings behind every hidden-field / AJAX payload the web layer embeds
/// for the client scripts to <c>JSON.parse</c>.
/// </summary>
public class JsonSerializationTests
{
    private record Sample(string Title, int Count);

    /// <summary>Property names are serialized verbatim (PascalCase) -- no camelCase naming policy is applied.</summary>
    [Fact]
    public void ToClientJson_KeepsPascalCasePropertyNames()
    {
        string json = JsonSerialization.ToClientJson(new Sample("Event", 3));

        Assert.Contains("\"Title\"", json);
        Assert.Contains("\"Count\"", json);
        Assert.DoesNotContain("\"title\"", json);
    }

    /// <summary>
    /// Non-ASCII text (e.g. a Korean event title) is left readable rather than escaped to \uXXXX --
    /// the relaxed encoder's whole point, per the field's own doc comment.
    /// </summary>
    [Fact]
    public void ToClientJson_LeavesNonAsciiTextUnescaped()
    {
        string json = JsonSerialization.ToClientJson(new Sample("생일 파티", 1));

        Assert.Contains("생일 파티", json);
        Assert.DoesNotContain("\\u", json);
    }

    /// <summary>Characters that would otherwise need escaping under the default encoder (e.g. '+') are also left as-is.</summary>
    [Fact]
    public void ToClientJson_LeavesPlusSignUnescaped()
    {
        string json = JsonSerialization.ToClientJson(new Sample("A+B", 1));

        Assert.Contains("A+B", json);
    }

    /// <summary>A null value serializes to the JSON literal "null", not an empty string.</summary>
    [Fact]
    public void ToClientJson_ReturnsJsonNull_ForNullValue()
    {
        string json = JsonSerialization.ToClientJson(null);

        Assert.Equal("null", json);
    }

    /// <summary>Output has no extra whitespace / indentation -- a single compact line.</summary>
    [Fact]
    public void ToClientJson_ProducesCompactOutput_NoIndentation()
    {
        string json = JsonSerialization.ToClientJson(new Sample("Event", 3));

        Assert.DoesNotContain("\n", json);
        Assert.DoesNotContain("  ", json);
    }
}
