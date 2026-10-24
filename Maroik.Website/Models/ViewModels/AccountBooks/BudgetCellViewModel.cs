namespace Maroik.Website.Models.ViewModels.AccountBooks;

/// <summary>A cell of the grid, ready to draw.</summary>
public sealed class BudgetCellViewModel
{
    /// <summary>Its A1 address.</summary>
    public string Address { get; init; } = "";

    /// <summary>Label, Budget, Computed, Blank or Free.</summary>
    public string Role { get; init; } = "";

    /// <summary>Its input in A1 form (shown in the formula bar), or empty.</summary>
    public string Input { get; init; } = "";

    /// <summary>Its value as shown.</summary>
    public string Display { get; init; } = "";

    /// <summary>Number, Text, Boolean, Empty or Error.</summary>
    public string ValueKind { get; init; } = "";

    /// <summary>For a budget cell, the actual total as shown; empty otherwise.</summary>
    public string Actual { get; init; } = "";

    /// <summary>For a budget cell with a budget, what remains as shown; empty otherwise.</summary>
    public string Remaining { get; init; } = "";

    /// <summary>For a budget cell, its colour class (good, warn, bad, unbudgeted) or empty.</summary>
    public string State { get; init; } = "";
}
