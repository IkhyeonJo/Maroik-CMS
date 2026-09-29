// ReSharper disable PropertyCanBeMadeInitOnly.Global
using System.ComponentModel.DataAnnotations;
using Maroik.Core.Domain.Finance;
using Maroik.Website.Mappings;

namespace Maroik.Website.Models.ViewModels.Notice;

/// <summary>
/// Read-only view model for displaying a fixed (recurring) expenditure record in the Notice area.
/// Includes computed flags (<see cref="Noticed"/>, <see cref="Expired"/>) that drive
/// alert badges on the dashboard and notification bell.
/// </summary>
public class FixedExpenditureOutputViewModel
{
    /// <summary>Unique database ID of this fixed expenditure record.</summary>
    [Display(Name = "Id")]
    public long Id { get; set; }

    /// <summary>Top-level expenditure category (e.g. "ConsumerSpending", "NonConsumerSpending").</summary>
    [Required(ErrorMessage = "Please enter MainClass")]
    [Display(Name = "MainClass")]
    public string? MainClass { get; set; }

    /// <summary>Sub-category within the main class (e.g. "ProtectionTypeInsurance", "Tax").</summary>
    [Required(ErrorMessage = "Please enter SubClass")]
    [Display(Name = "SubClass")]
    public string? SubClass { get; set; }

    /// <summary>Free-text description of this recurring spending entry.</summary>
    [Required(ErrorMessage = "Please enter Content")]
    [Display(Name = "Content")]
    public string? Content { get; set; }

    /// <summary>Fixed amount that is debited each recurrence.</summary>
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

    /// <summary>Name of the asset (product) credited by a transfer-type recurring expenditure; null for any other.</summary>
    [Required(ErrorMessage = "Please enter MyDepositAsset")]
    [Display(Name = "MyDepositAsset")]
    public string? MyDepositAsset { get; set; }

    /// <summary>Month of the year (1–12) on which this expenditure recurs.</summary>
    [Required(ErrorMessage = "Please enter DepositMonth")]
    [Display(Name = "DepositMonth")]
    public short DepositMonth { get; set; }

    /// <summary>Day of the month (1–31) on which this expenditure recurs.</summary>
    [Required(ErrorMessage = "Please enter DepositDay")]
    [Display(Name = "DepositDay")]
    public short DepositDay { get; set; }

    /// <summary>ISO-8601 date string (yyyy-MM-dd) after which this recurring expenditure is considered expired.</summary>
    [Required(ErrorMessage = "Please enter MaturityDate")]
    [Display(Name = "MaturityDate")]
    public string? MaturityDate { get; set; }

    /// <summary>When the record was first created, converted to the viewer's time zone.</summary>
    [Required(ErrorMessage = "Please enter Created")]
    [Display(Name = "Created")]
    public DateTime Created { get; set; }

    /// <summary>When this record was last updated, converted to the viewer's time zone.</summary>
    [Required(ErrorMessage = "Please enter Updated")]
    [Display(Name = "Updated")]
    public DateTime Updated { get; set; }

    /// <summary>Optional free-text memo for this fixed expenditure record.</summary>
    [Display(Name = "Note")]
    public string? Note { get; set; }

    /// <summary>
    /// When <see langword="true"/> the recurrence date is approaching (within the notification window),
    /// causing a badge to appear on the notification bell.
    /// </summary>
    public bool Noticed { get; set; }

    /// <summary>
    /// When <see langword="true"/> the maturity date has passed and no further recurrences are expected.
    /// </summary>
    public bool Expired { get; set; }

    /// <summary>
    /// User-chosen "always notify" flag: when <see langword="true"/> the schedule is always noticed, regardless of its date.
    /// </summary>
    [Display(Name = "Unpunctuality")]
    public bool Unpunctuality { get; set; }

    /// <summary>
    /// CSS row class for the grid, prioritizing Expired over Noticed over the default state.
    /// </summary>
    public string RowCssClass => FixedSchedulePolicy.GetRowStatus(Expired, Noticed).ToRowCssClass();
}
