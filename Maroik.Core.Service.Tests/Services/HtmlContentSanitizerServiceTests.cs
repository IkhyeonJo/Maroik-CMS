using Maroik.Core.Service.Services;

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// Unit tests for <see cref="HtmlContentSanitizerService"/>.
/// Verifies both the underlying Ganss.Xss defaults (script/event-handler stripping) and this
/// service's one customization (allowing the "class" attribute) — a regression in the latter
/// would silently widen the default XSS-safe allowlist with nothing to catch it.
/// </summary>
public class HtmlContentSanitizerServiceTests
{
    /// <summary>The service under test.</summary>
    private readonly HtmlContentSanitizerService _sut = new();

    /// <summary>Verifies that script tags are stripped.</summary>
    [Fact]
    public void Sanitize_RemovesScriptTags()
    {
        string result = _sut.Sanitize("<p>hello</p><script>alert('xss')</script>");

        Assert.DoesNotContain("<script", result, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies that inline event-handler attributes are stripped.</summary>
    [Fact]
    public void Sanitize_RemovesOnClickAttribute()
    {
        string result = _sut.Sanitize("<div onclick=\"alert('xss')\">click me</div>");

        Assert.DoesNotContain("onclick", result, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies the "class" attribute — explicitly widened beyond the library default — survives sanitization.</summary>
    [Fact]
    public void Sanitize_PreservesClassAttribute()
    {
        string result = _sut.Sanitize("<div class=\"highlight\">text</div>");

        Assert.Contains("class=\"highlight\"", result);
    }

    /// <summary>Verifies that plain safe text content passes through unchanged.</summary>
    [Fact]
    public void Sanitize_PreservesSafeTextContent()
    {
        string result = _sut.Sanitize("<p>plain paragraph</p>");

        Assert.Contains("plain paragraph", result);
    }
}
