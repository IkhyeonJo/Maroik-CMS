using Maroik.Core.Domain.Finance;

namespace Maroik.Core.Domain.Tests.Finance;

/// <summary>
/// The finance forms (account book and fixed income / expenditure) show an amount with a comma between
/// every three integer digits as it is typed ("1,234,567.5"), and post it in that form. <see cref="FinanceAmountPolicy.TryParseInput"/>
/// is the server's check of that text: digits with an optional leading minus, commas either absent or
/// exactly between groups of three integer digits, and an optional decimal part. Range and decimal
/// places are not its concern (the aggregates' <c>ValidateAmount</c> / <c>ValidateBalance</c> report
/// those), but it never rounds: text a <see cref="decimal"/> cannot hold exactly is refused.
/// </summary>
public class FinanceAmountInputTests
{
    /// <summary>Well-formed text parses to the value it shows.</summary>
    [Theory]
    [InlineData("0", "0")]
    [InlineData("7", "7")]
    [InlineData("999", "999")]
    [InlineData("1,000", "1000")]
    [InlineData("1,234,567", "1234567")]
    [InlineData("12,345,678.9012", "12345678.9012")]
    [InlineData("1234567", "1234567")]
    [InlineData("1234.5", "1234.5")]
    [InlineData("-1,234.5", "-1234.5")]
    [InlineData("0.0001", "0.0001")]
    [InlineData("0.00001", "0.00001")]
    [InlineData("1.50000000", "1.5")]
    [InlineData("1.", "1")]
    [InlineData("99,999,999,999,999,999", "99999999999999999")]
    public void TryParseInput_Accepts_WellFormedText(string input, string expected)
    {
        Assert.True(FinanceAmountPolicy.TryParseInput(input, out decimal amount));
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), amount);
    }

    /// <summary>Anything else is refused, and the out value is zero.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("-")]
    [InlineData(".5")]
    [InlineData("1,23")]
    [InlineData("1,2345")]
    [InlineData("12,34,567")]
    [InlineData("1234,567")]
    [InlineData(",123")]
    [InlineData("123,")]
    [InlineData("1,,234")]
    [InlineData("1.234,5")]
    [InlineData("1.2.3")]
    [InlineData("1.234,567")]
    [InlineData("+5")]
    [InlineData("--5")]
    [InlineData(" 5")]
    [InlineData("5 ")]
    [InlineData("5\n")]
    [InlineData("1e5")]
    [InlineData("0x10")]
    [InlineData("abc")]
    [InlineData("١٢٣")]
    [InlineData("１２３")]
    public void TryParseInput_Refuses_MalformedText(string? input)
    {
        Assert.False(FinanceAmountPolicy.TryParseInput(input, out decimal amount));
        Assert.Equal(0m, amount);
    }

    /// <summary>
    /// Text with more significant digits than a <see cref="decimal"/> holds exactly is refused instead of
    /// being rounded (a silently rounded amount is the drift <c>ValidateScale</c> exists to prevent).
    /// </summary>
    [Theory]
    [InlineData("0.00000000000000000000000000001")]
    [InlineData("1.0000000000000000000000000001")]
    [InlineData("100000000000000000000000000000")]
    [InlineData("100,000,000,000,000,000,000,000,000,000")]
    public void TryParseInput_Refuses_TextADecimalCannotHoldExactly(string input)
        => Assert.False(FinanceAmountPolicy.TryParseInput(input, out _));

    /// <summary>Up to 28 significant digits is exact, so it parses; zeros on either end do not count.</summary>
    [Theory]
    [InlineData("1,234,567,890,123,456,789,012,345,678", "1234567890123456789012345678")]
    [InlineData("0.1234567890123456789012345678", "0.1234567890123456789012345678")]
    [InlineData("000123.4500000000000000000000000000000000", "123.45")]
    public void TryParseInput_Accepts_UpToTwentyEightSignificantDigits(string input, string expected)
    {
        Assert.True(FinanceAmountPolicy.TryParseInput(input, out decimal amount));
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), amount);
    }
}
