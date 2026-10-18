using Maroik.Core.Domain.Tests.Fixtures;
using Maroik.Core.Domain.ValueObjects;

namespace Maroik.Core.Domain.Tests.ValueObjects;

/// <summary>
/// Equality, parsing and limits of the value objects: money compares amount and currency, the single-value objects
/// compare by value, e-mail parsing, and a currency code at its limit.
/// </summary>
public class ValueObjectBoundaryTests
{
    /// <summary>Owner e-mail used by every aggregate built in these tests.</summary>
    private const string Email = "user@example.com";

    /// <summary>A string of exactly <paramref name="length"/> characters.</summary>
    private static string Text(int length) => new('x', length);

    /// <summary>Verifies that two <c>Money</c> values are equal (with equal hash codes) only when amount and currency both match.</summary>
    [Fact]
    public void Money_EqualityConsidersBothAmountAndCurrency()
    {
        Money krw10 = Money.Create(10m, "KRW").Value;

        Assert.Equal(krw10, Money.Create(10m, "KRW").Value);
        Assert.Equal(krw10.GetHashCode(), Money.Create(10m, "KRW").Value.GetHashCode());
        Assert.NotEqual(krw10, Money.Create(10m, "USD").Value);
        Assert.NotEqual(krw10, Money.Create(11m, "KRW").Value);
    }

    /// <summary>Verifies value equality of color codes, time-zone IDs and emails (emails compare case-insensitively).</summary>
    [Fact]
    public void HtmlColorCodeTimeZoneIdAndEmail_AreEqualExactlyWhenTheirValuesAre()
    {
        Assert.Equal(HtmlColorCode.Create("#112233").Value, HtmlColorCode.Create("#112233").Value);
        Assert.NotEqual(HtmlColorCode.Create("#112233").Value, HtmlColorCode.Create("#445566").Value);
        Assert.Equal(TimeZoneId.Create("UTC").Value, TimeZoneId.Create("UTC").Value);
        Assert.NotEqual(TimeZoneId.Create("UTC").Value, TimeZoneId.Create("Asia/Seoul").Value);
        Assert.Equal(Domain.ValueObjects.Email.Create("a@b.com").Value, Domain.ValueObjects.Email.Create("A@B.com").Value);
        Assert.NotEqual(Domain.ValueObjects.Email.Create("a@b.com").Value, Domain.ValueObjects.Email.Create("c@b.com").Value);
    }

    /// <summary>Verifies that ordinary addresses, including dotted local parts and sub-domains, are accepted.</summary>
    [Theory]
    [InlineData("a@b.co")]
    [InlineData("first.last@sub.example.com")]
    public void Email_AcceptsOrdinaryAddresses(string address) => Assert.False(Domain.ValueObjects.Email.Create(address).IsError);

    /// <summary>Verifies that malformed hosts, local parts and the display-name form are rejected with <c>Email.Invalid</c>.</summary>
    [Theory]
    [InlineData("a@localhost")]     // host without a dot
    [InlineData("a@b..com")]        // empty host label
    [InlineData("a@.com")]
    [InlineData("a@b.c")]           // one-letter top-level domain
    [InlineData("a..b@x.com")]      // empty local-part label
    [InlineData(".a@x.com")]
    [InlineData("a.@x.com")]
    [InlineData("Jane <jane@x.com>")] // display-name form
    public void Email_RejectsMalformedAddresses(string address)
    {
        ErrorAssert.Validation(Domain.ValueObjects.Email.Create(address), "Email.Invalid", "'{0}' is not a valid email address.", address);
    }

    /// <summary>Verifies that a 45-character currency code is accepted, a 46-character or blank one is refused.</summary>
    [Fact]
    public void Money_Create_AcceptsACurrencyAtTheLimit_AndRefusesOneMore()
    {
        Assert.False(Money.Create(1m, Text(45)).IsError);

        ErrorAssert.Validation(Money.Create(1m, Text(46)), "Money.CurrencyTooLong", "Currency code must be {0} characters or fewer.", 45);
        ErrorAssert.Validation(Money.Create(1m, " "), "Money.CurrencyEmpty", "Currency code cannot be empty.");
    }

    /// <summary>Verifies that adding or subtracting mixed currencies reports both currency codes as template arguments.</summary>
    [Fact]
    public void Money_ArithmeticAcrossCurrencies_ReportsBothCodes()
    {
        Money krw = Money.Create(10m, "KRW").Value;
        Money usd = Money.Create(10m, "USD").Value;

        ErrorAssert.Validation(krw.Add(usd), "Money.CurrencyMismatch", "Cannot add {0} and {1}.", "KRW", "USD");
        ErrorAssert.Validation(krw.Subtract(usd), "Money.CurrencyMismatch", "Cannot subtract {0} from {1}.", "USD", "KRW");
    }
}
