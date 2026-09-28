using Maroik.Website.Extensions;

namespace Maroik.Website.Tests.Extensions;

/// <summary>
/// Unit tests for <see cref="Maroik.Website.Extensions.DecimalExtensions"/>.
/// Verifies trailing-zero trimming for <see cref="decimal"/> values
/// (e.g. <c>1000.00 → "1000"</c>) used when formatting currency amounts.
/// </summary>
public class DecimalExtensionsTests
{
    // -- TrimTrailingZeros (decimal) ------------------------------------------

    /// <summary>Verifies that <c>TrimTrailingZeros</c> formats whole number when without decimal point.</summary>
    [Theory]
    [InlineData(1000.00, "1000")]
    [InlineData(0.00, "0")]
    [InlineData(-500.00, "-500")]
    public void TrimTrailingZeros_FormatsWholeNumber_WithoutDecimalPoint(decimal value, string expected)
    {
        Assert.Equal(expected, value.TrimTrailingZeros());
    }

    /// <summary>Verifies that <c>TrimTrailingZeros</c> removes trailing decimal zeros.</summary>
    [Theory]
    [InlineData(3.14, "3.14")]
    [InlineData(1.50, "1.5")]
    [InlineData(0.1, "0.1")]
    [InlineData(100.10, "100.1")]
    public void TrimTrailingZeros_RemovesTrailingDecimalZeros(decimal value, string expected)
    {
        Assert.Equal(expected, value.TrimTrailingZeros());
    }

    /// <summary>Verifies that <c>TrimTrailingZeros</c> handles large whole number.</summary>
    [Fact]
    public void TrimTrailingZeros_HandlesLargeWholeNumber()
    {
        const decimal value = 1_000_000m;
        string result = value.TrimTrailingZeros();
        Assert.Equal("1000000", result);
    }

    /// <summary>Verifies that <c>TrimTrailingZeros</c> does not overflow for whole numbers beyond <see cref="int.MaxValue"/> (e.g. real-estate-scale KRW asset amounts), matching the column's <c>numeric(18,2)</c> range.</summary>
    [Fact]
    public void TrimTrailingZeros_HandlesWholeNumber_BeyondIntMaxValue()
    {
        const decimal value = 3_000_000_000m;
        string result = value.TrimTrailingZeros();
        Assert.Equal("3000000000", result);
    }

    // -- TrimTrailingZeros (decimal?) -----------------------------------------

    /// <summary>Verifies that <c>TrimTrailingZeros</c> nullable treats null as zero.</summary>
    [Fact]
    public void TrimTrailingZeros_NullableTreatsNullAsZero()
    {
        decimal? value = null;
        string result = value.TrimTrailingZeros();
        Assert.Equal("0", result);
    }

    /// <summary>Verifies that <c>TrimTrailingZeros</c> (nullable overload) behaves identically to the non-nullable overload when the value is non-null.</summary>
    [Theory]
    [InlineData(500.00, "500")]
    [InlineData(3.14, "3.14")]
    [InlineData(2.50, "2.5")]
    public void TrimTrailingZeros_Nullable_BehavesLikeNonNullable_WhenHasValue(decimal value, string expected)
    {
        decimal? nullable = value;
        Assert.Equal(expected, nullable.TrimTrailingZeros());
    }

    /// <summary>Verifies that <c>TrimTrailingZeros</c> (nullable overload) does not include a decimal point for whole-number values.</summary>
    [Fact]
    public void TrimTrailingZeros_Nullable_WholeNumber_DoesNotShowDecimalPoint()
    {
        decimal? value = 999m;
        Assert.Equal("999", value.TrimTrailingZeros());
    }
}
