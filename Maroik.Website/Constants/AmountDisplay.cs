using Maroik.Core.Domain.Finance;

namespace Maroik.Website.Constants;

/// <summary>
/// How the finance screens show an amount: with a thousands separator and every decimal place the column stores
/// (<see cref="FinanceAmountPolicy.MaxDecimalPlaces"/>), trailing zeros dropped — 10000 → "10,000", 12.50 → "12.5",
/// 1234.5678 → "1,234.5678". The currency is a free-text label, so its usual number of decimals cannot be looked
/// up; showing what was stored never hides a digit the user entered. A presentation setting, so it lives here
/// rather than in the domain (like <see cref="CulturePolicy"/>).
/// </summary>
public static class AmountDisplay
{
    /// <summary>The .NET custom numeric format, e.g. <c>"#,0.####"</c>.</summary>
    public static readonly string Format = "#,0." + new string('#', FinanceAmountPolicy.MaxDecimalPlaces);

    /// <summary><see cref="Format"/> as a composite format, for MvcGrid's <c>Formatted(...)</c>.</summary>
    public static readonly string GridFormat = "{0:" + Format + "}";
}
