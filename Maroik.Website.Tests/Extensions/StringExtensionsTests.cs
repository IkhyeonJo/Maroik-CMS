using Maroik.Website.Extensions;

namespace Maroik.Website.Tests.Extensions;

/// <summary>
/// Unit tests for <see cref="Maroik.Website.Extensions.StringExtensions"/>.
/// Verifies the <c>GetAmountLabel</c> helper that formats a decimal value
/// with its currency unit for display in the dashboard and account-book views.
/// </summary>
public class StringExtensionsTests
{
    // -- GetAmountLabel -------------------------------------------------------

    /// <summary>Verifies that <c>GetAmountLabel</c> returns formatted label with value and unit.</summary>
    [Fact]
    public void GetAmountLabel_ReturnsFormattedLabel_WithValueAndUnit()
    {
        string result = "Amount".GetAmountLabel("KRW");

        Assert.Equal("Amount (KRW)", result);
    }

    /// <summary>Verifies that <c>GetAmountLabel</c> includes empty parentheses when monetary unit is empty.</summary>
    [Fact]
    public void GetAmountLabel_IncludesEmptyParentheses_WhenMonetaryUnitIsEmpty()
    {
        string result = "Amount".GetAmountLabel("");

        Assert.Equal("Amount ()", result);
    }

    /// <summary>Verifies that <c>GetAmountLabel</c> includes empty parentheses when monetary unit is null.</summary>
    [Fact]
    public void GetAmountLabel_IncludesEmptyParentheses_WhenMonetaryUnitIsNull()
    {
        string result = "Amount".GetAmountLabel(null!);

        Assert.Equal("Amount ()", result);
    }

    /// <summary>Verifies that <c>GetAmountLabel</c> works with various units.</summary>
    [Fact]
    public void GetAmountLabel_WorksWithVariousUnits()
    {
        Assert.Equal("잔액 (USD)", "잔액".GetAmountLabel("USD"));
        Assert.Equal("Balance (EUR)", "Balance".GetAmountLabel("EUR"));
        Assert.Equal("금액 (JPY)", "금액".GetAmountLabel("JPY"));
    }
}
