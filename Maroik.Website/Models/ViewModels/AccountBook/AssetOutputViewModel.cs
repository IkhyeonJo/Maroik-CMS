// ReSharper disable PropertyCanBeMadeInitOnly.Global
using System.ComponentModel.DataAnnotations;

namespace Maroik.Website.Models.ViewModels.AccountBook;

/// <summary>
/// Read-only view model representing a single asset record as displayed in list or detail views.
/// Includes all persisted fields including audit timestamps and the soft-delete flag.
/// </summary>
public class AssetOutputViewModel
{
    /// <summary>Name of the financial product ?? forms part of the composite primary key.</summary>
    [Required(ErrorMessage = "Please enter ProductName")]
    [Display(Name = "ProductName")]
    public string? ProductName { get; set; }

    /// <summary>Asset category / type (e.g. "Deposit", "Stock").</summary>
    [Required(ErrorMessage = "Please enter Item")]
    [Display(Name = "Item")]
    public string? Item { get; set; }

    /// <summary>Current balance or market value of the asset.</summary>
    [Required(ErrorMessage = "Please enter Amount")]
    [Display(Name = "Amount")]
    public decimal Amount { get; set; }

    /// <summary>Currency code for the amount (e.g. "KRW", "USD").</summary>
    [Required(ErrorMessage = "Please enter MonetaryUnit")]
    [Display(Name = "MonetaryUnit")]
    public string? MonetaryUnit { get; set; }

    /// <summary>UTC timestamp when the asset record was first created.</summary>
    [Required(ErrorMessage = "Please enter Created")]
    [Display(Name = "Created")]
    public DateTime Created { get; set; }

    /// <summary>UTC timestamp of the most recent update to this asset record.</summary>
    [Required(ErrorMessage = "Please enter Updated")]
    [Display(Name = "Updated")]
    public DateTime Updated { get; set; }

    /// <summary>Optional free-text memo about the asset.</summary>
    [Required(ErrorMessage = "Please enter Note")]
    [Display(Name = "Note")]
    public string? Note { get; set; }

    /// <summary>Soft-delete flag; when <see langword="true"/> the asset is excluded from normal views.</summary>
    [Display(Name = "Deleted")]
    public bool Deleted { get; set; }
}
