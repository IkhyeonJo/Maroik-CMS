namespace Maroik.Website.Models.ViewModels.AccountBooks;

/// <summary>A sheet, ready to draw.</summary>
public sealed class BudgetSheetViewModel
{
    /// <summary>Income or Expenditure.</summary>
    public string Kind { get; init; } = "";

    /// <summary>The year.</summary>
    public int Year { get; init; }

    /// <summary>False until the sheet is first edited.</summary>
    public bool Exists { get; init; }

    /// <summary>The rows.</summary>
    public List<BudgetRowViewModel> Rows { get; init; } = [];

    /// <summary>The cells with something to show.</summary>
    public List<BudgetCellViewModel> Cells { get; init; } = [];

    /// <summary>Records of lines the sheet does not have.</summary>
    public List<BudgetUnbudgetedViewModel> Unbudgeted { get; init; } = [];
}
