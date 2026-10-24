namespace Maroik.Core.Contract.Dtos;

/// <summary>A budget sheet as shown.</summary>
public sealed class BudgetSheetResponse
{
    /// <summary>Income or Expenditure.</summary>
    public string Kind { get; init; } = "";

    /// <summary>The year.</summary>
    public int Year { get; init; }

    /// <summary>False until the sheet is first edited.</summary>
    public bool Exists { get; init; }

    /// <summary>The rows.</summary>
    public List<BudgetRowResponse> Rows { get; init; } = [];

    /// <summary>The cells with something to show.</summary>
    public List<BudgetCellResponse> Cells { get; init; } = [];

    /// <summary>Records of lines the sheet does not have.</summary>
    public List<BudgetUnbudgetedResponse> Unbudgeted { get; init; } = [];
}
