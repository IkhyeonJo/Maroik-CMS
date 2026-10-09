using Maroik.Core.Domain.ValueObjects;
namespace Maroik.Core.Domain.Tests.ValueObjects;

/// <summary>
/// Unit tests for <see cref="Maroik.Core.Domain.ValueObjects.HtmlColorCode"/>.
/// Covers creation with valid 6-digit hex codes, upper-case normalization, empty/invalid format
/// rejections (including the 3-digit CSS shorthand, which is valid CSS but not accepted here — see
/// <see cref="Create_ReturnsError_WhenInvalidFormat"/>), and equality.
/// </summary>
public class HtmlColorCodeTests
{
    /// <summary>Create returns code and normalizes to upper case.</summary>
    [Theory]
    [InlineData("#FF5733", "#FF5733")]
    [InlineData("#ff5733", "#FF5733")]
    [InlineData("#AABBCC", "#AABBCC")]
    [InlineData("#aabbcc", "#AABBCC")]
    public void Create_ReturnsCode_AndNormalizesToUpperCase(string input, string expected)
    {
        var result = HtmlColorCode.Create(input);

        Assert.False(result.IsError);
        Assert.Equal(expected, result.Value.Value);
    }

    /// <summary>Create returns error, when empty.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ReturnsError_WhenEmpty(string? input)
    {
        var result = HtmlColorCode.Create(input);

        Assert.True(result.IsError);
        Assert.Equal("HtmlColorCode.Empty", result.FirstError.Code);
    }

    /// <summary>
    /// Create returns error, when invalid format. Includes the 3-digit CSS shorthand ("#ABC"): it is
    /// valid CSS but rejected here because <c>Calendar_HtmlColorCode_check</c> (the DB CHECK this
    /// value is ultimately persisted against) only allows the 6-digit form — accepting it here would
    /// let the Domain/Service layers pass a value the repository then rejects with a raw DB
    /// exception instead of this clean validation error.
    /// </summary>
    [Theory]
    [InlineData("FF5733")]
    [InlineData("#GGGGGG")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("#ABC")]
    [InlineData("#abc")]
    [InlineData("red")]
    [InlineData("#FF5733\n")]
    [InlineData("#FF5733\r\n")]
    [InlineData("#FF5733 ")]
    [InlineData("\n#FF5733")]
    public void Create_ReturnsError_WhenInvalidFormat(string input)
    {
        var result = HtmlColorCode.Create(input);

        Assert.True(result.IsError);
        Assert.Equal("HtmlColorCode.Invalid", result.FirstError.Code);
    }

    /// <summary>
    /// The "invalid format" error is built via <c>DomainError</c>: its Metadata carries the
    /// composite-format resource template and the raw offending value separately, so
    /// <c>ServiceResult.FromError</c> can hand the UI something resx-localizable instead of the
    /// already-baked, value-embedding sentence in <see cref="ErrorOr.Error.Description"/>.
    /// </summary>
    [Fact]
    public void Create_ReturnsError_WithLocalizableMetadata_WhenInvalidFormat()
    {
        var result = HtmlColorCode.Create("red");

        Assert.Equal("'red' is not a valid HTML hex color code (e.g. #FF5733).", result.FirstError.Description);
        Assert.Equal("'{0}' is not a valid HTML hex color code (e.g. #FF5733).", result.FirstError.Metadata!["MessageTemplate"]);
        Assert.Equal(["red"], (object[])result.FirstError.Metadata["MessageArgs"]);
    }

    /// <summary>Equality same value are equal.</summary>
    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        var a = HtmlColorCode.Create("#FF0000").Value;
        var b = HtmlColorCode.Create("#ff0000").Value;

        Assert.Equal(a, b);
    }

    /// <summary>ToString is the color code.</summary>
    [Fact]
    public void ToString_IsTheColorCode()
    {
        Assert.Equal("#3788D8", HtmlColorCode.Create("#3788d8").Value.ToString()); // normalized to upper case
    }
}
