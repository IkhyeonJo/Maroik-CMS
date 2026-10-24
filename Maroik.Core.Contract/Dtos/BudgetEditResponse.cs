namespace Maroik.Core.Contract.Dtos;

/// <summary>The outcome of an edit: the edited sheet's cells whose value or input changed.</summary>
public sealed class BudgetEditResponse
{
    /// <summary>The changed cells.</summary>
    public List<BudgetCellResponse> Changed { get; init; } = [];

    /// <summary>How many cells the edit wrote.</summary>
    public int Written { get; init; }
}
