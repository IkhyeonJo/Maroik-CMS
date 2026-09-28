using Maroik.Core.Domain.ValueObjects;
namespace Maroik.Core.Domain.Tests.ValueObjects;

/// <summary>
/// Unit tests for <see cref="Maroik.Core.Domain.ValueObjects.TimeZoneId"/>.
/// Covers acceptance of valid IANA zone identifiers, rejection of empty and unrecognized values, and equality.
/// </summary>
public class TimeZoneIdTests
{
    /// <summary>Create returns time zone id, when valid.</summary>
    [Theory]
    [InlineData("UTC")]
    [InlineData("Asia/Seoul")]
    [InlineData("America/New_York")]
    public void Create_ReturnsTimeZoneId_WhenValid(string input)
    {
        var result = TimeZoneId.Create(input);

        Assert.False(result.IsError);
        Assert.Equal(input, result.Value.Value);
    }

    /// <summary>Create returns error, when empty.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ReturnsError_WhenEmpty(string? input)
    {
        var result = TimeZoneId.Create(input);

        Assert.True(result.IsError);
        Assert.Equal("TimeZoneId.Empty", result.FirstError.Code);
    }

    /// <summary>Create returns error, when unrecognized.</summary>
    [Fact]
    public void Create_ReturnsError_WhenUnrecognised()
    {
        var result = TimeZoneId.Create("Not/AReal/TimeZone");

        Assert.True(result.IsError);
        Assert.Equal("TimeZoneId.Invalid", result.FirstError.Code);
        Assert.Equal("'Not/AReal/TimeZone' is not a recognised time-zone ID.", result.FirstError.Description);
    }

    /// <summary>
    /// The "unrecognized" error is built via <c>LocalizableError</c>: its Metadata carries the
    /// composite-format resource template and the raw offending value separately, so
    /// <c>ServiceResult.FromError</c> can hand the UI something resx-localizable instead of the
    /// already-baked, value-embedding sentence in <see cref="ErrorOr.Error.Description"/>.
    /// </summary>
    [Fact]
    public void Create_ReturnsError_WithLocalizableMetadata_WhenUnrecognised()
    {
        var result = TimeZoneId.Create("Not/AReal/TimeZone");

        Assert.Equal("'{0}' is not a recognised time-zone ID.", result.FirstError.Metadata!["ResourceKey"]);
        Assert.Equal(["Not/AReal/TimeZone"], (object[])result.FirstError.Metadata["ResourceArgs"]);
    }

    /// <summary>Equality same value are equal.</summary>
    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        var a = TimeZoneId.Create("UTC").Value;
        var b = TimeZoneId.Create("UTC").Value;

        Assert.Equal(a, b);
    }

    /// <summary>ToString is the IANA id.</summary>
    [Fact]
    public void ToString_IsTheIanaId()
    {
        Assert.Equal("Asia/Seoul", TimeZoneId.Create("Asia/Seoul").Value.ToString());
    }
}
