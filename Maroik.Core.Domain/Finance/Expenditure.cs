using ErrorOr;
using Maroik.Core.Domain.Localization;
using Maroik.Core.Domain.Primitives;
using Maroik.Core.Domain.ValueObjects;

namespace Maroik.Core.Domain.Finance;

/// <summary>
/// Aggregate root representing a single one-time expenditure record.
/// The payment method and deposit-asset references point to <see cref="Asset"/> identities
/// (ProductName) — they are not object references, preserving aggregate boundaries.
/// </summary>
public sealed class Expenditure : AggregateRoot<long>
{
    /// <summary>Email of the account that owns this record.</summary>
    public Email AccountEmail { get; private set; }

    /// <summary>Top-level expense category (e.g. "ConsumerSpending", "NonConsumerSpending").</summary>
    public string MainClass { get; private set; }

    /// <summary>Detailed expense sub-category (e.g. "MealOrEatOutExpenses", "Tax").</summary>
    public string SubClass { get; private set; }

    /// <summary>Description of what was spent on.</summary>
    public string? Content { get; private set; }

    /// <summary>Amount spent.</summary>
    public Money Amount { get; private set; }

    /// <summary>Asset product name the amount is debited from (reference by identity, not object).</summary>
    public string PaymentMethod { get; private set; }

    /// <summary>
    /// Asset product name credited by a transfer-type expenditure (a subClass in
    /// <see cref="ExpenditureClassPolicy.DepositAssetSubClasses"/>, e.g. savings or debt repayment) —
    /// the money leaves <see cref="PaymentMethod"/> and arrives here. Must differ from
    /// <see cref="PaymentMethod"/>; null for every other expenditure.
    /// </summary>
    public string? MyDepositAsset { get; private set; }

    /// <summary>UTC timestamp when the expenditure occurred.</summary>
    public DateTime Created { get; private set; }

    /// <summary>UTC timestamp of the most recent edit.</summary>
    public DateTime Updated { get; private set; }

    /// <summary>Optional free-text note.</summary>
    public string? Note { get; private set; }

    /// <summary>"New record" constructor: stamps <see cref="Created"/>/<see cref="Updated"/>. Used by <see cref="Record"/> only.</summary>
    private Expenditure(
        long id,
        Email accountEmail,
        string mainClass,
        string subClass,
        string? content,
        Money amount,
        string paymentMethod,
        string? myDepositAsset,
        string? note,
        DateTime utcNow) : base(id)
    {
        AccountEmail = accountEmail;
        MainClass = mainClass;
        SubClass = subClass;
        Content = content;
        Amount = amount;
        PaymentMethod = paymentMethod;
        MyDepositAsset = myDepositAsset;
        Note = note;
        Created = utcNow;
        Updated = utcNow;
    }

    /// <summary>Reconstitution constructor: assigns every field verbatim from trusted storage with no new timestamps.</summary>
    private Expenditure(
        long id, Email accountEmail, string mainClass, string subClass, string? content, Money amount,
        string paymentMethod, string? myDepositAsset, string? note, DateTime created, DateTime updated) : base(id)
    {
        AccountEmail = accountEmail;
        MainClass = mainClass;
        SubClass = subClass;
        Content = content;
        Amount = amount;
        PaymentMethod = paymentMethod;
        MyDepositAsset = myDepositAsset;
        Note = note;
        Created = created;
        Updated = updated;
    }

    // ------------------------------------------------------------------------
    // Factory / Reconstitution
    // ------------------------------------------------------------------------

    /// <summary>
    /// Rebuilds an <see cref="Expenditure"/> from trusted raw values from the repository layer.
    /// Bypasses validation — use only with data from the trusted repository layer.
    /// </summary>
    public static Expenditure Reconstitute(
        long id,
        string accountEmail,
        string mainClass,
        string subClass,
        string? content,
        decimal amount,
        string monetaryUnit,
        string paymentMethod,
        string? myDepositAsset,
        string? note,
        DateTime created,
        DateTime updated)
    {
        return new Expenditure(
            id,
            Email.FromTrustedSource(accountEmail),
            mainClass,
            subClass,
            content,
            Money.FromTrustedSource(amount, monetaryUnit),
            paymentMethod,
            myDepositAsset,
            note,
            created,
            updated);
    }

    /// <summary>
    /// Records a new expenditure.
    /// </summary>
    public static ErrorOr<Expenditure> Record(
        string? accountEmailValue,
        string? mainClass,
        string? subClass,
        string? content,
        decimal amount,
        string? currency,
        string? paymentMethod,
        string? myDepositAsset,
        DateTime utcNow,
        string? note = null,
        DateTime? created = null)
    {
        var coreResult = ValidateCoreFields(mainClass, subClass, paymentMethod, content, amount, note);
        if (coreResult.IsError) return coreResult.Errors;

        var emailResult = Email.Create(accountEmailValue);
        if (emailResult.IsError) return emailResult.Errors;

        var moneyResult = Money.Create(amount, currency);
        if (moneyResult.IsError) return moneyResult.Errors;

        (string validMainClass, string validSubClass, string validPaymentMethod) = coreResult.Value;
        var expenditure = new Expenditure(0, emailResult.Value, validMainClass, validSubClass, content,
            moneyResult.Value, validPaymentMethod, myDepositAsset, note, utcNow);

        if (created.HasValue)
            expenditure.Created = created.Value;

        return expenditure;
    }

    /// <summary>
    /// Shared validation for the fields <see cref="Record"/> and <see cref="Update"/> both take,
    /// returning the three required strings once confirmed non-blank so callers keep the compiler's
    /// non-null flow. Each entry point still applies its own remaining rules — <see cref="Email"/>
    /// on create, and the <see cref="Money"/> value — in its own order.
    /// </summary>
    private static ErrorOr<(string MainClass, string SubClass, string PaymentMethod)> ValidateCoreFields(
        string? mainClass, string? subClass, string? paymentMethod, string? content, decimal amount, string? note)
    {
        if (string.IsNullOrWhiteSpace(mainClass))
            return LocalizableError.Validation("Expenditure.MainClassEmpty", "Main expense category cannot be empty.");

        if (string.IsNullOrWhiteSpace(subClass))
            return LocalizableError.Validation("Expenditure.SubClassEmpty", "Sub expense category cannot be empty.");

        if (string.IsNullOrWhiteSpace(paymentMethod))
            return LocalizableError.Validation("Expenditure.PaymentMethodEmpty", "Payment method (asset name) cannot be empty.");

        // Content is NOT NULL / non-blank at the DB (Expenditure_Content_check: Content ~ '\S') and
        // [Required] on the ViewModel; mirror that here so a blank value is a clean validation
        // error rather than a raw 23514 from the database.
        if (string.IsNullOrWhiteSpace(content))
            return LocalizableError.Validation("Expenditure.ContentEmpty", "Content cannot be empty.");

        var amountResult = FinanceAmountPolicy.ValidateAmount(amount);
        if (amountResult.IsError) return amountResult.Errors;

        var textResult = FinanceTextPolicy.ValidateContentAndNote(content, note);
        if (textResult.IsError) return textResult.Errors;

        var classResult = ExpenditureClassPolicy.Validate(mainClass, subClass);
        if (classResult.IsError) return classResult.Errors;

        return (mainClass, subClass, paymentMethod);
    }

    // ------------------------------------------------------------------------
    // Domain behaviours
    // ------------------------------------------------------------------------

    /// <summary>Updates all editable fields of this expenditure record.</summary>
    public ErrorOr<Success> Update(
        string? mainClass,
        string? subClass,
        string? content,
        decimal amount,
        string? currency,
        string? paymentMethod,
        string? myDepositAsset,
        string? note,
        DateTime utcNow,
        DateTime? created = null)
    {
        var coreResult = ValidateCoreFields(mainClass, subClass, paymentMethod, content, amount, note);
        if (coreResult.IsError) return coreResult.Errors;

        var moneyResult = Money.Create(amount, currency);
        if (moneyResult.IsError) return moneyResult.Errors;

        (string validMainClass, string validSubClass, string validPaymentMethod) = coreResult.Value;
        MainClass = validMainClass;
        SubClass = validSubClass;
        Content = content;
        Amount = moneyResult.Value;
        PaymentMethod = validPaymentMethod;
        MyDepositAsset = myDepositAsset;
        Note = note;
        if (created.HasValue)
            Created = created.Value;
        Updated = utcNow;
        return Result.Success;
    }
}
