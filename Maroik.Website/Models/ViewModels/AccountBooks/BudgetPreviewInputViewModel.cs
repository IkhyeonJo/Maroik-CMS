using System.ComponentModel.DataAnnotations;

namespace Maroik.Website.Models.ViewModels.AccountBooks;

/// <summary>An input being typed into a cell, to colour and preview.</summary>
public sealed class BudgetPreviewInputViewModel : BudgetSheetInputViewModel
{
    /// <summary>The cell's A1 address.</summary>
    [Required]
    public string Address { get; set; } = "";

    /// <summary>What is typed so far.</summary>
    public string? Input { get; set; }
}
