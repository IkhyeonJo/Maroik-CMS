// ReSharper disable UnusedAutoPropertyAccessor.Global
using System.ComponentModel.DataAnnotations;

namespace Maroik.Website.Models.ViewModels.Notice;

/// <summary>
/// Form model for creating or editing a fixed (recurring) income entry.
/// Fixed incomes recur on a specific day of a specific month and have a maturity date.
/// The <see cref="Unpunctuality"/> flag is the user's "always notify" choice for an income whose timing is not punctual.
/// </summary>
public class FixedIncomeInputViewModel
{
    /// <summary>Database row ID; 0 for a new record, positive for an edit.</summary>
    [Display(Name = "Id")]
    public int Id { get; set; }

    /// <summary>Top-level income category (e.g. "RegularIncome", "IrregularIncome").</summary>
    [Required(ErrorMessage = "Please enter MainClass")]
    [Display(Name = "MainClass")]
    public string? MainClass { get; set; }

    /// <summary>Sub-category within the main class (e.g. "LaborIncome", "PensionIncome").</summary>
    [Required(ErrorMessage = "Please enter SubClass")]
    [Display(Name = "SubClass")]
    public string? SubClass { get; set; }

    /// <summary>Free-text description of this recurring income entry.</summary>
    [Required(ErrorMessage = "Please enter Content")]
    [Display(Name = "Content")]
    public string? Content { get; set; }

    /// <summary>Expected fixed amount deposited each recurrence.</summary>
    [Required(ErrorMessage = "Please enter Amount")]
    [Display(Name = "Amount")]
    public decimal Amount { get; set; }

    /// <summary>Month of the year (1–12) on which this income is scheduled to recur.</summary>
    [Required(ErrorMessage = "Please enter DepositMonth")]
    [Display(Name = "DepositMonth")]
    public byte DepositMonth { get; set; }

    /// <summary>Day of the month (1–31) on which this income is expected to arrive.</summary>
    [Required(ErrorMessage = "Please enter DepositDay")]
    [Display(Name = "DepositDay")]
    public byte DepositDay { get; set; }

    /// <summary>ISO-8601 date string (yyyy-MM-dd) after which this recurring income expires.</summary>
    [Required(ErrorMessage = "Please enter MaturityDate")]
    [Display(Name = "MaturityDate")]
    public string? MaturityDate { get; set; }

    /// <summary>Optional free-text memo for this fixed income record.</summary>
    [Display(Name = "Note")]
    public string? Note { get; set; }

    /// <summary>Name of the asset (product) into which this recurring income is deposited.</summary>
    [Required(ErrorMessage = "Please enter DepositMyAssetProductName")]
    [Display(Name = "DepositMyAssetProductName")]
    public string? DepositMyAssetProductName { get; set; }

    /// <summary>
    /// User-chosen "always notify" flag: when <see langword="true"/> the schedule is always counted as
    /// noticed (highlighted on the Notice page and in the header badge), regardless of its date.
    /// </summary>
    [Display(Name = "Unpunctuality")]
    public bool Unpunctuality { get; set; }
}
