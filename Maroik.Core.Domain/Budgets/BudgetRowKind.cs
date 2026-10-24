namespace Maroik.Core.Domain.Budgets;

/// <summary>What a row of a budget sheet is.</summary>
public enum BudgetRowKind
{
    /// <summary>A main class's header row.</summary>
    Header,

    /// <summary>A line: one sub-class in one currency.</summary>
    Line,

    /// <summary>A main class's subtotal in one currency.</summary>
    Subtotal,

    /// <summary>The sheet's total in one currency.</summary>
    Total,

    /// <summary>A free row for helper values and notes.</summary>
    Free,
}
