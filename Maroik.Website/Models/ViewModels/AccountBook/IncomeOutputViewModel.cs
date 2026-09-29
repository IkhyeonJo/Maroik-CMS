// ReSharper disable PropertyCanBeMadeInitOnly.Global
using System.ComponentModel.DataAnnotations;

namespace Maroik.Website.Models.ViewModels.AccountBook;

/// <summary>
/// Read-only view model for displaying a single income transaction in list or detail views.
/// Includes all persisted fields plus the currency unit for formatted display.
/// </summary>
public class IncomeOutputViewModel
{
    /// <summary>Unique database ID of this income record.</summary>
    [Display(Name = "Id")]
    public long Id { get; set; }

    /// <summary>Top-level income category (e.g. "RegularIncome", "IrregularIncome").</summary>
    [Required(ErrorMessage = "Please enter MainClass")]
    [Display(Name = "MainClass")]
    public string? MainClass { get; set; }

    /// <summary>Sub-category within the main class (e.g. "LaborIncome", "FinancialIncome").</summary>
    [Required(ErrorMessage = "Please enter SubClass")]
    [Display(Name = "SubClass")]
    public string? SubClass { get; set; }

    /// <summary>Free-text description of this income entry.</summary>
    [Required(ErrorMessage = "Please enter Content")]
    [Display(Name = "Content")]
    public string? Content { get; set; }

    /// <summary>Amount of income received.</summary>
    [Required(ErrorMessage = "Please enter Amount")]
    [Display(Name = "Amount")]
    public decimal Amount { get; set; }

    /// <summary>Currency code for the amount (e.g. "KRW", "USD"), sourced from the linked asset.</summary>
    [Display(Name = "MonetaryUnit")]
    public string? MonetaryUnit { get; set; }

    /// <summary>Name of the asset (product) into which this income was deposited.</summary>
    [Required(ErrorMessage = "Please enter DepositMyAssetProductName")]
    [Display(Name = "DepositMyAssetProductName")]
    public string? DepositMyAssetProductName { get; set; }

    /// <summary>When the income was recorded, converted to the viewer's time zone.</summary>
    [Required(ErrorMessage = "Please enter Created")]
    [Display(Name = "Created")]
    public DateTime Created { get; set; }

    /// <summary>When this income record was last updated, converted to the viewer's time zone.</summary>
    [Required(ErrorMessage = "Please enter Updated")]
    [Display(Name = "Updated")]
    public DateTime Updated { get; set; }

    /// <summary>Optional free-text memo for this income record.</summary>
    [Display(Name = "Note")]
    public string? Note { get; set; }
}
