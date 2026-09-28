// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Per-subClass amount and percentage-of-total breakdown for one main class (e.g. "RegularIncome")
/// over one reporting period, computed by <c>DashboardService</c> via
/// <see cref="Maroik.Core.Domain.Finance.FinanceBreakdownPolicy"/>. The dashboard view model mapper
/// only reads values out of this — it never sums or divides.
/// </summary>
public class FinanceBreakdownDto
{
    /// <summary>Sum of every record's amount under this main class, whatever its subClass.</summary>
    public decimal Total { get; set; }

    /// <summary>Summed amount per subClass actually present.</summary>
    public Dictionary<string, decimal> AmountBySubClass { get; set; } = new();

    /// <summary>Each subClass's share of <see cref="Total"/> as 0-100. Empty when <see cref="Total"/> is zero.</summary>
    public Dictionary<string, double> PercentageBySubClass { get; set; } = new();
}
