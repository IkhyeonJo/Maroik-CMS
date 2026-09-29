// ReSharper disable PropertyCanBeMadeInitOnly.Global
using System.ComponentModel.DataAnnotations;

namespace Maroik.Website.Models.ViewModels.AccountBook;

/// <summary>
/// Read-only view model for displaying a single expenditure transaction in list or detail views.
/// Includes all persisted fields plus the currency unit for formatted display.
/// </summary>
public class ExpenditureOutputViewModel
{
    /// <summary>Unique database ID of this expenditure record.</summary>
    [Display(Name = "Id")]
    public long Id { get; set; }

    /// <summary>Top-level expenditure category (e.g. "ConsumerSpending", "NonConsumerSpending").</summary>
    [Required(ErrorMessage = "Please enter MainClass")]
    [Display(Name = "MainClass")]
    public string? MainClass { get; set; }

    /// <summary>Sub-category within the main class (e.g. "MealOrEatOutExpenses", "Tax").</summary>
    [Required(ErrorMessage = "Please enter SubClass")]
    [Display(Name = "SubClass")]
    public string? SubClass { get; set; }

    /// <summary>Free-text description of this spending entry.</summary>
    [Required(ErrorMessage = "Please enter Content")]
    [Display(Name = "Content")]
    public string? Content { get; set; }

    /// <summary>Amount spent.</summary>
    [Required(ErrorMessage = "Please enter Amount")]
    [Display(Name = "Amount")]
    public decimal Amount { get; set; }

    /// <summary>Currency code for the amount (e.g. "KRW", "USD"), sourced from the linked asset.</summary>
    [Display(Name = "MonetaryUnit")]
    public string? MonetaryUnit { get; set; }

    /// <summary>Name of the asset (product) debited to pay for this expenditure — an FK to
    /// <c>Asset.ProductName</c>, despite the field name (e.g. "CreditCard", "Cash").</summary>
    [Required(ErrorMessage = "Please enter PaymentMethod")]
    [Display(Name = "PaymentMethod")]
    public string? PaymentMethod { get; set; }

    /// <summary>Optional free-text memo for this expenditure record.</summary>
    [Display(Name = "Note")]
    public string? Note { get; set; }

    /// <summary>Name of the asset (product) credited by a transfer-type expenditure; null for any other expenditure.</summary>
    [Required(ErrorMessage = "Please enter MyDepositAsset")]
    [Display(Name = "MyDepositAsset")]
    public string? MyDepositAsset { get; set; }

    /// <summary>When the expenditure was recorded, converted to the viewer's time zone.</summary>
    [Required(ErrorMessage = "Please enter Created")]
    [Display(Name = "Created")]
    public DateTime Created { get; set; }

    /// <summary>When this expenditure record was last updated, converted to the viewer's time zone.</summary>
    [Required(ErrorMessage = "Please enter Updated")]
    [Display(Name = "Updated")]
    public DateTime Updated { get; set; }
}
