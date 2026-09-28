// ReSharper disable UnusedAutoPropertyAccessor.Global
using System.ComponentModel.DataAnnotations;

namespace Maroik.Website.Models.ViewModels.AccountBook;

/// <summary>
/// Form model for creating or editing a personal asset (bank account / investment product).
/// The asset is identified by the combination of <see cref="ProductName"/> and the
/// logged-in account's email, so renaming requires the original name to be stored in
/// <see cref="OriginalProductName"/> for the cascade-update to work correctly.
/// </summary>
public class AssetInputViewModel
{
    /// <summary>Name of the financial product (e.g. "My Savings Account") ?? part of the composite PK.</summary>
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

    /// <summary>Optional free-text memo about the asset.</summary>
    [Display(Name = "Note")]
    public string? Note { get; set; }

    /// <summary>Soft-delete flag; when <see langword="true"/> the asset is hidden from normal views.</summary>
    [Display(Name = "Deleted")]
    public bool Deleted { get; set; }

    /// <summary>
    /// Captures the asset's name before an edit so the service layer can cascade-rename
    /// all income and expenditure records that reference this product name.
    /// </summary>
    public string? OriginalProductName { get; set; }
}
