using System.ComponentModel.DataAnnotations;

namespace Maroik.Website.Models.ViewModels.AccountBooks;

/// <summary>One typed cell edit.</summary>
public sealed class BudgetCellEditInputViewModel
{
    /// <summary>The cell's A1 address.</summary>
    [Required]
    public string Address { get; set; } = "";

    /// <summary>The new input (empty clears the cell).</summary>
    public string? Input { get; set; }
}
