namespace Maroik.Core.Contract.Dtos;

/// <summary>Records of a line the sheet does not have.</summary>
public sealed class BudgetUnbudgetedResponse
{
    /// <summary>The main class.</summary>
    public string MainClass { get; init; } = "";

    /// <summary>The sub-class.</summary>
    public string SubClass { get; init; } = "";

    /// <summary>The currency.</summary>
    public string Currency { get; init; } = "";

    /// <summary>The twelve monthly totals.</summary>
    public List<decimal> Months { get; init; } = [];
}
