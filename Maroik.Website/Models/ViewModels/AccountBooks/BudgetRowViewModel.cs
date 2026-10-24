namespace Maroik.Website.Models.ViewModels.AccountBooks;

/// <summary>A row of the grid.</summary>
public sealed class BudgetRowViewModel
{
    /// <summary>Its row number.</summary>
    public int Number { get; init; }

    /// <summary>Header, Line, Subtotal, Total or Free.</summary>
    public string Kind { get; init; } = "";

    /// <summary>Its localized label (column A of a fixed row).</summary>
    public string Label { get; init; } = "";

    /// <summary>Its main class (for removing a line), or empty.</summary>
    public string MainClass { get; init; } = "";

    /// <summary>Its sub-class, or empty.</summary>
    public string SubClass { get; init; } = "";

    /// <summary>Its currency, or empty.</summary>
    public string Currency { get; init; } = "";

    /// <summary>The letters of the columns a user can edit in this row.</summary>
    public string Editable { get; init; } = "";
}
