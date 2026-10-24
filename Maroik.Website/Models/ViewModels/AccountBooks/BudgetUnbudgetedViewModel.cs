namespace Maroik.Website.Models.ViewModels.AccountBooks;

/// <summary>Records of a line the sheet does not have.</summary>
public sealed class BudgetUnbudgetedViewModel
{
    /// <summary>The localized sub-class.</summary>
    public string Label { get; init; } = "";

    /// <summary>The main class.</summary>
    public string MainClass { get; init; } = "";

    /// <summary>The sub-class.</summary>
    public string SubClass { get; init; } = "";

    /// <summary>The currency.</summary>
    public string Currency { get; init; } = "";

    /// <summary>The twelve monthly totals as shown.</summary>
    public List<string> Months { get; init; } = [];
}
