// ReSharper disable UnusedAutoPropertyAccessor.Global
using System.ComponentModel.DataAnnotations;
using Maroik.Core.Domain.Finance;
// ReSharper disable PropertyCanBeMadeInitOnly.Global

namespace Maroik.Website.Models.ViewModels.Notice;

/// <summary>
/// Form model for creating or editing a fixed (recurring) expenditure entry.
/// Fixed expenditures repeat on a specific day of a specific month and have a maturity date
/// after which they are treated as expired. An <see cref="Unpunctuality"/> flag tracks
/// whether the payment was made on time.
/// </summary>
public class FixedExpenditureInputViewModel : IValidatableObject
{
    /// <summary>Database row ID; 0 for a new record, positive for an edit.</summary>
    [Display(Name = "Id")]
    public int Id { get; set; }

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

    /// <summary>Month of the year (1–12) on which this expenditure is scheduled to recur.</summary>
    [Required(ErrorMessage = "Please enter DepositMonth")]
    [Display(Name = "DepositMonth")]
    public byte DepositMonth { get; set; }

    /// <summary>Day of the month (1–31) on which this expenditure is scheduled to recur.</summary>
    [Required(ErrorMessage = "Please enter DepositDay")]
    [Display(Name = "DepositDay")]
    public byte DepositDay { get; set; }

    /// <summary>ISO-8601 date string (yyyy-MM-dd) after which this recurring expenditure expires.</summary>
    [Required(ErrorMessage = "Please enter MaturityDate")]
    [Display(Name = "MaturityDate")]
    public string? MaturityDate { get; set; }

    /// <summary>Optional free-text memo for this fixed expenditure record.</summary>
    [Display(Name = "Note")]
    public string? Note { get; set; }

    /// <summary>Name of the asset (product) debited to pay for this expenditure — an FK to
    /// <c>Asset.ProductName</c>, despite the field name (e.g. "CreditCard", "Cash").</summary>
    [Required(ErrorMessage = "Please enter PaymentMethod")]
    [Display(Name = "PaymentMethod")]
    public string? PaymentMethod { get; set; }

    /// <summary>
    /// Name of the asset (product) that is debited for this recurring expenditure. Required only
    /// when <see cref="SubClass"/> is one of
    /// <see cref="ExpenditureClassPolicy.DepositAssetSubClasses"/> (a transfer-type subclass) —
    /// the domain only needs a deposit asset then; see <see cref="Validate"/>.
    /// </summary>
    [Display(Name = "MyDepositAsset")]
    public string? MyDepositAsset { get; set; }

    /// <summary>
    /// When <see langword="true"/> the payment was not made on the scheduled date (late or missed).
    /// Used in the Notice page to highlight overdue fixed expenditures.
    /// </summary>
    [Display(Name = "Unpunctuality")]
    public bool Unpunctuality { get; set; }

    /// <summary>
    /// Enforces that <see cref="MyDepositAsset"/> is supplied when (and only when) the domain
    /// actually requires one, instead of a blanket <c>[Required]</c> that would reject a valid
    /// non-transfer fixed expenditure from any caller that correctly omits it.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (SubClass != null && ExpenditureClassPolicy.DepositAssetSubClasses.Contains(SubClass)
            && string.IsNullOrWhiteSpace(MyDepositAsset))
        {
            yield return new ValidationResult("Please enter MyDepositAsset", [nameof(MyDepositAsset)]);
        }
    }
}
