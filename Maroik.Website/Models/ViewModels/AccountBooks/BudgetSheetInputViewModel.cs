using System.ComponentModel.DataAnnotations;

namespace Maroik.Website.Models.ViewModels.AccountBooks;

/// <summary>Which sheet a request is about.</summary>
public class BudgetSheetInputViewModel
{
    /// <summary>Income or Expenditure.</summary>
    [Required]
    [RegularExpression("^(Income|Expenditure)$")]
    public string Kind { get; set; } = "";

    /// <summary>The year.</summary>
    public int Year { get; set; }
}
