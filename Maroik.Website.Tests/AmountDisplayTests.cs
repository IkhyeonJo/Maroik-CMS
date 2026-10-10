using System.Globalization;
using Maroik.Core.Domain.Finance;
using Maroik.Website.Constants;

namespace Maroik.Website.Tests;

/// <summary>Unit tests for <see cref="AmountDisplay"/>.</summary>
public class AmountDisplayTests
{
    /// <summary>
    /// An amount shows every decimal place the column stores (up to <see cref="FinanceAmountPolicy.MaxDecimalPlaces"/>),
    /// without trailing zeros, and with a thousands separator — the same in every supported culture.
    /// </summary>
    [Theory]
    [InlineData("en-US", "10000", "10,000")]
    [InlineData("en-US", "12.5", "12.5")]
    [InlineData("en-US", "12.50", "12.5")]
    [InlineData("en-US", "1.234", "1.234")]
    [InlineData("en-US", "1234.5678", "1,234.5678")]
    [InlineData("en-US", "0.0001", "0.0001")]
    [InlineData("en-US", "0", "0")]
    [InlineData("en-US", "-1234.5", "-1,234.5")]
    [InlineData("ko-KR", "1234.5678", "1,234.5678")]
    [InlineData("ko-KR", "10000", "10,000")]
    public void Format_ShowsUpToTheStoredDecimalPlaces_WithAThousandsSeparator(string culture, string amount, string expected)
        => Assert.Equal(expected, decimal.Parse(amount, CultureInfo.InvariantCulture).ToString(AmountDisplay.Format, new CultureInfo(culture)));

    /// <summary>The grid's composite format renders the same text as <see cref="AmountDisplay.Format"/>.</summary>
    [Fact]
    public void GridFormat_RendersLikeFormat()
        => Assert.Equal("1,234.5678", string.Format(new CultureInfo("en-US"), AmountDisplay.GridFormat, 1234.5678m));
}
