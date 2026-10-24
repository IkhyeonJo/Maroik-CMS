namespace Maroik.Core.Contract.Dtos;

/// <summary>A cell of a budget sheet as shown.</summary>
public sealed class BudgetCellResponse
{
    /// <summary>Its A1 address.</summary>
    public string Address { get; init; } = "";

    /// <summary>Label, Budget, Computed, Blank or Free.</summary>
    public string Role { get; init; } = "";

    /// <summary>Its input in A1 form, or null.</summary>
    public string? Input { get; init; }

    /// <summary>Its value.</summary>
    public BudgetValueResponse Value { get; init; } = new();

    /// <summary>For a budget cell: the budget (null without a number).</summary>
    public decimal? Budget { get; init; }

    /// <summary>For a budget cell: the actual total; null for other cells.</summary>
    public decimal? Actual { get; init; }

    /// <summary>For a budget cell: budget minus actual (null without a budget).</summary>
    public decimal? Remaining { get; init; }

    /// <summary>For a budget cell: None, Unbudgeted, Under, Reached or Over; null for other cells.</summary>
    public string? State { get; init; }
}
