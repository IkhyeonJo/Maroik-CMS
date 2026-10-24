using System.ComponentModel.DataAnnotations;

namespace Maroik.Website.Models.ViewModels.AccountBooks;

/// <summary>A paste or fill: the copied block and where it goes.</summary>
public sealed class BudgetCopyInputViewModel : BudgetSheetInputViewModel
{
    /// <summary>The copied block ("B2" or "B2:C3").</summary>
    [Required]
    public string Source { get; set; } = "";

    /// <summary>The target block.</summary>
    [Required]
    public string Target { get; set; } = "";
}
