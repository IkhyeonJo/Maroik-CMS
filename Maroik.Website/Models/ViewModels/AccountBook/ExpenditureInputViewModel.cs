// ReSharper disable UnusedAutoPropertyAccessor.Global
using System.ComponentModel.DataAnnotations;
using Maroik.Core.Domain.Finance;
using Maroik.Website.Mappings;
// ReSharper disable PropertyCanBeMadeInitOnly.Global

namespace Maroik.Website.Models.ViewModels.AccountBook;

/// <summary>
/// Form model for creating or editing a single expenditure transaction in the account book.
/// The spending is classified by <see cref="MainClass"/> / <see cref="SubClass"/>
/// (e.g. "ConsumerSpending" / "MealOrEatOutExpenses") and linked to the asset it is paid from
/// (<see cref="PaymentMethod"/>) and, for a transfer, the asset it is credited to (<see cref="MyDepositAsset"/>).
/// </summary>
public class ExpenditureInputViewModel : IValidatableObject
{
    /// <summary>Database row ID; 0 for a new record, positive for an edit.</summary>
    [Display(Name = "Id")]
    public int Id { get; set; }

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

    /// <summary>
    /// The local date/time the expenditure occurred, as "yyyy-MM-dd HH:mm:ss" in the account's own
    /// time zone (never the browser's) — the controller converts it to UTC using the logged-in
    /// account's <c>TimeZoneIanaId</c>, already known server-side from the session, so the client
    /// never needs to compute or transmit a UTC instant or a time zone itself.
    /// </summary>
    [Display(Name = "Created")]
    public string? Created { get; set; }

    /// <summary>Name of the asset (product) debited to pay for this expenditure — an FK to
    /// <c>Asset.ProductName</c>, despite the field name (e.g. "CreditCard", "Cash").</summary>
    [Required(ErrorMessage = "Please enter PaymentMethod")]
    [Display(Name = "PaymentMethod")]
    public string? PaymentMethod { get; set; }

    /// <summary>Optional free-text memo for this expenditure record.</summary>
    [Display(Name = "Note")]
    public string? Note { get; set; }

    /// <summary>
    /// Name of the asset (product) credited by this expenditure — the transfer target. Required only when
    /// <see cref="SubClass"/> is one of <see cref="ExpenditureClassPolicy.DepositAssetSubClasses"/>
    /// (a transfer-type subclass) — the domain only needs a deposit asset then; see
    /// <see cref="Validate"/>.
    /// </summary>
    [Display(Name = "MyDepositAsset")]
    public string? MyDepositAsset { get; set; }

    /// <summary>
    /// Enforces that <see cref="MyDepositAsset"/> is supplied when (and only when) the domain
    /// actually requires one, instead of a blanket <c>[Required]</c> that would reject a valid
    /// non-transfer expenditure from any caller that correctly omits it.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (SubClass != null && ExpenditureClassPolicy.DepositAssetSubClasses.Contains(SubClass)
            && string.IsNullOrWhiteSpace(MyDepositAsset))
        {
            yield return new ValidationResult("Please enter MyDepositAsset", [nameof(MyDepositAsset)]);
        }

        if (!AccountBookViewModelMapper.IsValidLocalDateTime(Created))
        {
            yield return new ValidationResult("Please enter a valid Created", [nameof(Created)]);
        }
    }
}
