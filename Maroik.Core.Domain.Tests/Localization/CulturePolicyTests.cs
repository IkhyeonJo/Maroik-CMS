using Maroik.Core.Domain.Localization;
namespace Maroik.Core.Domain.Tests.Localization;

/// <summary>Unit tests for <see cref="CulturePolicy"/>.</summary>
public class CulturePolicyTests
{
    /// <summary>Korean culture pre-selects Asia/Seoul; anything else leaves the choice to the user.</summary>
    [Theory]
    [InlineData("ko-KR", "Asia/Seoul")]
    [InlineData("en-US", null)]
    [InlineData("en-GB", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void DefaultTimeZoneIanaId_MapsKoreanOnly(string? culture, string? expected)
    {
        Assert.Equal(expected, CulturePolicy.DefaultTimeZoneIanaId(culture));
    }
}
