using System.ComponentModel.DataAnnotations;

namespace Maroik.Website.Models.ViewModels.AccountBooks;

/// <summary>A line to add or remove.</summary>
public sealed class BudgetLineInputViewModel : BudgetSheetInputViewModel
{
    /// <summary>The main class.</summary>
    [Required]
    public string MainClass { get; set; } = "";

    /// <summary>The sub-class.</summary>
    [Required]
    public string SubClass { get; set; } = "";

    /// <summary>The currency label.</summary>
    [Required]
    public string Currency { get; set; } = "";
}
