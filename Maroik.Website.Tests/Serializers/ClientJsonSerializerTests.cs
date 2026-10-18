using Maroik.Website.Serializers;
// ReSharper disable NotAccessedPositionalProperty.Local

namespace Maroik.Website.Tests.Serializers;

/// <summary>
/// Unit tests for <see cref="ClientJsonSerializer.Serialize"/> -- the shared
/// <c>System.Text.Json</c> settings behind every hidden-field / AJAX payload the web layer embeds
/// for the client scripts to <c>JSON.parse</c>.
/// </summary>
public class ClientJsonSerializerTests
{
    /// <summary>A small object to serialize.</summary>
    private record Sample(string Title, int Count);

    /// <summary>Property names are serialized verbatim (PascalCase) -- no camelCase naming policy is applied.</summary>
    [Fact]
    public void Serialize_KeepsPascalCasePropertyNames()
    {
        string json = ClientJsonSerializer.Serialize(new Sample("Event", 3));

        Assert.Contains("\"Title\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Count\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"title\"", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// Non-ASCII text (e.g. a Korean event title) is left readable rather than escaped to \uXXXX --
    /// the relaxed encoder's whole point, per the field's own doc comment.
    /// </summary>
    [Fact]
    public void Serialize_LeavesNonAsciiTextUnescaped()
    {
        string json = ClientJsonSerializer.Serialize(new Sample("생일 파티", 1));

        Assert.Contains("생일 파티", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u", json, StringComparison.Ordinal);
    }

    /// <summary>Characters that would otherwise need escaping under the default encoder (e.g. '+') are also left as-is.</summary>
    [Fact]
    public void Serialize_LeavesPlusSignUnescaped()
    {
        string json = ClientJsonSerializer.Serialize(new Sample("A+B", 1));

        Assert.Contains("A+B", json, StringComparison.Ordinal);
    }

    /// <summary>A null value serializes to the JSON literal "null", not an empty string.</summary>
    [Fact]
    public void Serialize_ReturnsJsonNull_ForNullValue()
    {
        string json = ClientJsonSerializer.Serialize(null);

        Assert.Equal("null", json);
    }

    /// <summary>Output has no extra whitespace / indentation -- a single compact line.</summary>
    [Fact]
    public void Serialize_ProducesCompactOutput_NoIndentation()
    {
        string json = ClientJsonSerializer.Serialize(new Sample("Event", 3));

        Assert.DoesNotContain("\n", json, StringComparison.Ordinal);
        Assert.DoesNotContain("  ", json, StringComparison.Ordinal);
    }
}
