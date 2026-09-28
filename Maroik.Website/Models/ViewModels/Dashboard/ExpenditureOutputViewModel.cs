// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable PropertyCanBeMadeInitOnly.Global
using System.ComponentModel.DataAnnotations;

namespace Maroik.Website.Models.ViewModels.Dashboard;

/// <summary>
/// Read-only view model for a single expenditure record displayed on the Dashboard.
/// Mirrors <c>AccountBook.ExpenditureOutputViewModel</c> but lives in the Dashboard
/// namespace so it can be used independently in the yearly/monthly breakdown tables.
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

    /// <summary>Name of the asset (product) that was debited for this expenditure.</summary>
    [Required(ErrorMessage = "Please enter MyDepositAsset")]
    [Display(Name = "MyDepositAsset")]
    public string? MyDepositAsset { get; set; }

    /// <summary>UTC timestamp when the expenditure was recorded.</summary>
    [Required(ErrorMessage = "Please enter Created")]
    [Display(Name = "Created")]
    public DateTime Created { get; set; }

    /// <summary>UTC timestamp of the most recent update to this expenditure record.</summary>
    [Required(ErrorMessage = "Please enter Updated")]
    [Display(Name = "Updated")]
    public DateTime Updated { get; set; }
}
