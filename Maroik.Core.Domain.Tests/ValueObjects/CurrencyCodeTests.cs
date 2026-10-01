using Maroik.Core.Domain.ValueObjects;
namespace Maroik.Core.Domain.Tests.ValueObjects;

/// <summary>
/// Unit tests for <see cref="CurrencyCode"/>: a trimmed, upper-cased currency label of at most 45
/// characters (normally ISO 4217, but any label such as "원" is accepted).
/// </summary>
public class CurrencyCodeTests
{
    /// <summary>A label is trimmed and upper-cased.</summary>
    [Theory]
    [InlineData(" krw ", "KRW")]
    [InlineData("usd", "USD")]
    [InlineData("원", "원")]
    public void Create_NormalizesTheLabel(string input, string expected)
        => Assert.Equal(expected, CurrencyCode.Create(input).Value.Value);

    /// <summary>Two spellings of the same code are equal.</summary>
    [Fact]
    public void Codes_AreEqual_AfterNormalization()
        => Assert.Equal(CurrencyCode.Create("krw").Value, CurrencyCode.Create(" KRW").Value);

    /// <summary>An empty label is refused.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsAnEmptyLabel(string? input)
        => Assert.Equal("Money.CurrencyEmpty", CurrencyCode.Create(input).FirstError.Code);

    /// <summary>A label longer than the persisted column (45) is refused; 45 is accepted.</summary>
    [Fact]
    public void Create_RejectsALabelLongerThanTheColumn()
    {
        Assert.False(CurrencyCode.Create(new string('A', 45)).IsError);
        var error = CurrencyCode.Create(new string('A', 46)).FirstError;
        Assert.Equal("Money.CurrencyTooLong", error.Code);
        Assert.Equal("Currency code must be 45 characters or fewer.", error.Description);
    }

    /// <summary>The code renders as its value.</summary>
    [Fact]
    public void ToString_ReturnsTheValue() => Assert.Equal("KRW", CurrencyCode.Create("krw").Value.ToString());
}
