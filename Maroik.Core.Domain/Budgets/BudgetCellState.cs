namespace Maroik.Core.Domain.Budgets;

/// <summary>Where a budget cell's actual total stands against its budget.</summary>
public enum BudgetCellState
{
    /// <summary>No budget and nothing recorded.</summary>
    None,

    /// <summary>Records but no budget.</summary>
    Unbudgeted,

    /// <summary>The actual total is below the budget.</summary>
    Under,

    /// <summary>The actual total equals the budget.</summary>
    Reached,

    /// <summary>The actual total is above the budget.</summary>
    Over,
}
