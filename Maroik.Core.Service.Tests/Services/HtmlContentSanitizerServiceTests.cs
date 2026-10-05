using System.Text.RegularExpressions;
using Maroik.Core.Service.Services;

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// Unit tests for <see cref="HtmlContentSanitizerService"/>.
/// Verifies both the underlying Ganss.Xss defaults (script/event-handler stripping) and this
/// service's customizations (allowing the "class" attribute, removing the overlay CSS properties)
/// — a regression in either would silently change the allowlist with nothing to catch it.
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

    /// <summary>An overlay a user could post: a full-screen, invisible link stacked above the page.</summary>
    private const string OverlayLink =
        "<a href=\"https://example.com\" style=\"position:fixed;top:0;left:0;right:0;bottom:0;"
        + "width:100%;height:100%;z-index:9999;opacity:0\">x</a>";

    /// <summary>
    /// Verifies each layout property that lets stored content escape its box and cover the page
    /// (a click-jacking link or a fake login overlay) is stripped from inline styles.
    /// </summary>
    [Theory]
    [InlineData("position")]
    [InlineData("top")]
    [InlineData("left")]
    [InlineData("right")]
    [InlineData("bottom")]
    [InlineData("z-index")]
    [InlineData("opacity")]
    public void Sanitize_RemovesOverlayCssProperty(string property)
    {
        string result = _sut.Sanitize(OverlayLink);

        Assert.DoesNotMatch(StyleDeclaration(property), result);
    }

    /// <summary>Verifies stripping the overlay properties keeps the link and its harmless sizing.</summary>
    [Fact]
    public void Sanitize_KeepsTheLinkAndItsSizing_WhenOverlayPropertiesAreStripped()
    {
        string result = _sut.Sanitize(OverlayLink);

        Assert.Contains("href=\"https://example.com\"", result);
        Assert.Matches(StyleDeclaration("width"), result);
        Assert.Matches(StyleDeclaration("height"), result);
    }

    /// <summary>Verifies the inline formatting Summernote emits survives sanitization.</summary>
    [Theory]
    [InlineData("color", "<span style=\"color: rgb(255, 0, 0)\">x</span>")]
    [InlineData("background-color", "<span style=\"background-color: rgb(255, 255, 0)\">x</span>")]
    [InlineData("font-size", "<span style=\"font-size: 18px\">x</span>")]
    [InlineData("font-family", "<span style=\"font-family: Arial\">x</span>")]
    [InlineData("font-weight", "<span style=\"font-weight: bold\">x</span>")]
    [InlineData("text-align", "<p style=\"text-align: left\">x</p>")]
    [InlineData("line-height", "<p style=\"line-height: 1.5\">x</p>")]
    [InlineData("margin-left", "<p style=\"margin-left: 25px\">x</p>")]
    [InlineData("float", "<img src=\"https://example.com/a.png\" style=\"float: left; width: 50%\">")]
    public void Sanitize_PreservesSummernoteFormatting(string property, string html)
    {
        string result = _sut.Sanitize(html);

        Assert.Matches(StyleDeclaration(property), result);
    }

    /// <summary>Matches one <paramref name="property"/> declaration inside an inline style.</summary>
    private static Regex StyleDeclaration(string property) =>
        new($"style=\"(?:[^\"]*[;\\s])?{Regex.Escape(property)}\\s*:", RegexOptions.IgnoreCase);

    /// <summary>Stored content cannot carry <c>data-*</c> attributes (script hooks for client widgets).</summary>
    [Fact]
    public void Sanitize_RemovesDataAttributes()
    {
        string result = _sut.Sanitize("<div data-toggle=\"modal\" data-target=\"#x\">text</div>");

        Assert.DoesNotContain("data-", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("text", result);
    }

    /// <summary>Links and images keep plain http and https addresses.</summary>
    [Theory]
    [InlineData("<a href=\"http://example.com/\">x</a>", "href=\"http://example.com/\"")]
    [InlineData("<a href=\"https://example.com/\">x</a>", "href=\"https://example.com/\"")]
    [InlineData("<img src=\"http://example.com/a.png\">", "src=\"http://example.com/a.png\"")]
    public void Sanitize_KeepsHttpAndHttpsUrls(string html, string expected)
    {
        Assert.Contains(expected, _sut.Sanitize(html));
    }

    /// <summary>Any other URL scheme is dropped from links and images.</summary>
    [Theory]
    [InlineData("<a href=\"javascript:alert(1)\">x</a>")]
    [InlineData("<a href=\"mailto:someone@example.com\">x</a>")]
    [InlineData("<a href=\"ftp://example.com/f\">x</a>")]
    [InlineData("<img src=\"data:image/png;base64,AAAA\">")]
    public void Sanitize_DropsUrlsWithAnyOtherScheme(string html)
    {
        string result = _sut.Sanitize(html);

        Assert.DoesNotContain("href=", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("src=", result, StringComparison.OrdinalIgnoreCase);
    }
}
