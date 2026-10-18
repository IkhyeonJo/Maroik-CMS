// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Aggregated data object used to populate the user dashboard page.
/// Carries the income and expenditure breakdowns and the asset data for the selected year and month.
/// </summary>
public class DashboardDto
{
    /// <summary>The user's preferred currency unit, used to filter displayed amounts.</summary>
    public string? DefaultMonetaryUnit { get; set; }

    /// <summary>Per-main-class income breakdown (subclass amounts + percentages), keyed by main class, for the selected year.</summary>
    public Dictionary<string, FinanceBreakdownDto> YearIncomeBreakdown { get; set; } = new();

    /// <summary>Per-main-class income breakdown for the selected year + month.</summary>
    public Dictionary<string, FinanceBreakdownDto> YearMonthIncomeBreakdown { get; set; } = new();

    /// <summary>Per-main-class expenditure breakdown for the selected year.</summary>
    public Dictionary<string, FinanceBreakdownDto> YearExpenditureBreakdown { get; set; } = new();

    /// <summary>Per-main-class expenditure breakdown for the selected year + month.</summary>
    public Dictionary<string, FinanceBreakdownDto> YearMonthExpenditureBreakdown { get; set; } = new();

    /// <summary>All active assets owned by the user.</summary>
    public List<AssetResponse> Assets { get; set; } = [];

    /// <summary>Distinct currency units from the user's assets (used to populate the filter dropdown).</summary>
    public IEnumerable<string?> MonetaryUnits { get; set; } = [];

    /// <summary>Earliest year for which the user has income or expenditure data (used to build the year selector).</summary>
    public int StartYear { get; set; }

    /// <summary>Latest year for which the user has data.</summary>
    public int EndYear { get; set; }

    /// <summary>Currently selected year for filtering.</summary>
    public int SelectedYear { get; set; }

    /// <summary>Currently selected month (1–12) for filtering.</summary>
    public int SelectedMonth { get; set; }
}
