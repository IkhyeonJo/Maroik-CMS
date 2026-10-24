namespace Maroik.Core.Domain.Budgets;

/// <summary>What a cell of a budget sheet is, and so whether and how it can be edited.</summary>
public enum BudgetCellRole
{
    /// <summary>Column A of a fixed row: the row's label (read-only).</summary>
    Label,

    /// <summary>A line's month cell: its value is that month's budget (a number or a formula).</summary>
    Budget,

    /// <summary>A sum the sheet computes (a line's year total, a subtotal, a total; read-only).</summary>
    Computed,

    /// <summary>A header row's B..N cell (read-only, empty).</summary>
    Blank,

    /// <summary>A free cell: a number, text, a boolean or a formula.</summary>
    Free,
}
