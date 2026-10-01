using ErrorOr;
using Maroik.Core.Domain.Localization;
using Maroik.Core.Domain.Primitives;
using Maroik.Core.Domain.ValueObjects;

namespace Maroik.Core.Domain.Finance;

/// <summary>
/// Aggregate root for a recurring fixed expenditure (e.g. subscription, insurance premium).
/// Scheduled on a month/day (<see cref="DepositMonth"/>/<see cref="DepositDay"/>) until the
/// <see cref="MaturityDate"/>; <see cref="FixedSchedulePolicy"/> decides when it is due for notice.
/// </summary>
public sealed class FixedExpenditure : AggregateRoot<long>
{
    /// <summary>Email of the owning account.</summary>
    public Email AccountEmail { get; private set; }

    /// <summary>Top-level expense category.</summary>
    public string MainClass { get; private set; }

    /// <summary>Detailed expense sub-category.</summary>
    public string SubClass { get; private set; }

    /// <summary>Description of the recurring expense (e.g. "Netflix subscription").</summary>
    public string? Content { get; private set; }

    /// <summary>Recurring charge amount.</summary>
    public Money Amount { get; private set; }

    /// <summary>Asset product name the charge is paid from (reference by identity).</summary>
    public string PaymentMethod { get; private set; }

    /// <summary>
    /// Asset product name credited by a transfer-type charge (a subClass in
    /// <see cref="ExpenditureClassPolicy.DepositAssetSubClasses"/>); must differ from
    /// <see cref="PaymentMethod"/>. Null for every other charge.
    /// </summary>
    public string? MyDepositAsset { get; private set; }

    /// <summary>Month (1–12) of the scheduled charge date.</summary>
    public short DepositMonth { get; private set; }

    /// <summary>Day of <see cref="DepositMonth"/> the charge is due (validated by <see cref="FixedSchedulePolicy.IsValidDepositDate"/>).</summary>
    public short DepositDay { get; private set; }

    /// <summary>Date when the recurring charge ends (contract / subscription expiry).</summary>
    public DateTime MaturityDate { get; private set; }

    /// <summary>UTC timestamp when the record was created.</summary>
    public DateTime Created { get; private set; }

    /// <summary>UTC timestamp of the most recent update.</summary>
    public DateTime Updated { get; private set; }

    /// <summary>Optional free-text note.</summary>
    public string? Note { get; private set; }

    /// <summary>
    /// User-chosen "always notify" flag for a charge whose timing is not punctual: when true the
    /// schedule is always noticed, regardless of its date (see <see cref="FixedSchedulePolicy.IsNoticed"/>).
    /// </summary>
    public bool Unpunctuality { get; private set; }

    /// <summary>"New record" constructor: stamps <see cref="Created"/>/<see cref="Updated"/>. Used by <see cref="Register"/> only.</summary>
    private FixedExpenditure(
        long id,
        Email accountEmail,
        string mainClass,
        string subClass,
        string? content,
        Money amount,
        string paymentMethod,
        string? myDepositAsset,
        short depositMonth,
        short depositDay,
        DateTime maturityDate,
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
        DepositMonth = depositMonth;
        DepositDay = depositDay;
        MaturityDate = maturityDate;
        Note = note;
        Created = utcNow;
        Updated = utcNow;
    }

    /// <summary>Reconstitution constructor: assigns every field verbatim from trusted storage with no new timestamps.</summary>
    private FixedExpenditure(
        long id, Email accountEmail, string mainClass, string subClass, string? content, Money amount,
        string paymentMethod, string? myDepositAsset, short depositMonth, short depositDay, DateTime maturityDate,
        string? note, bool unpunctuality, DateTime created, DateTime updated) : base(id)
    {
        AccountEmail = accountEmail;
        MainClass = mainClass;
        SubClass = subClass;
        Content = content;
        Amount = amount;
        PaymentMethod = paymentMethod;
        MyDepositAsset = myDepositAsset;
        DepositMonth = depositMonth;
        DepositDay = depositDay;
        MaturityDate = maturityDate;
        Note = note;
        Unpunctuality = unpunctuality;
        Created = created;
        Updated = updated;
    }

    // ------------------------------------------------------------------------
    // Factory / Reconstitution
    // ------------------------------------------------------------------------

    /// <summary>
    /// Rebuilds a <see cref="FixedExpenditure"/> from trusted raw values from the repository layer.
    /// Bypasses validation — use only with data from the trusted repository layer.
    /// </summary>
    public static FixedExpenditure Reconstitute(
        long id,
        string accountEmail,
        string mainClass,
        string subClass,
        string? content,
        decimal amount,
        string monetaryUnit,
        string paymentMethod,
        string? myDepositAsset,
        short depositMonth,
        short depositDay,
        DateTime maturityDate,
        string? note,
        bool unpunctuality,
        DateTime created,
        DateTime updated)
    {
        return new FixedExpenditure(
            id,
            Email.FromTrustedSource(accountEmail),
            mainClass,
            subClass,
            content,
            Money.FromTrustedSource(amount, monetaryUnit),
            paymentMethod,
            myDepositAsset,
            depositMonth,
            depositDay,
            maturityDate,
            note,
            unpunctuality,
            created,
            updated);
    }

    /// <summary>
    /// Registers a new fixed expenditure.
    /// </summary>
    public static ErrorOr<FixedExpenditure> Register(
        string? accountEmailValue,
        string? mainClass,
        string? subClass,
        string? content,
        decimal amount,
        string? currency,
        string? paymentMethod,
        string? myDepositAsset,
        short depositMonth,
        short depositDay,
        DateTime maturityDate,
        DateTime utcNow,
        string? note = null)
    {
        var coreResult = ValidateCoreFields(mainClass, subClass, paymentMethod, depositMonth, depositDay, amount, content, note);
        if (coreResult.IsError) return coreResult.Errors;

        var emailResult = Email.Create(accountEmailValue);
        if (emailResult.IsError) return emailResult.Errors;

        var moneyResult = Money.Create(amount, currency);
        if (moneyResult.IsError) return moneyResult.Errors;

        if (!FixedSchedulePolicy.IsAcceptableMaturityDate(maturityDate, utcNow))
            return LocalizableError.Validation("FixedExpenditure.MaturityDateInPast",
                "The maturity date cannot be earlier than the current date.");

        var (validMainClass, validSubClass, validPaymentMethod) = coreResult.Value;
        var fixedExpenditure = new FixedExpenditure(0, emailResult.Value, validMainClass, validSubClass, content,
            moneyResult.Value, validPaymentMethod, myDepositAsset, depositMonth, depositDay, maturityDate, note, utcNow);

        return fixedExpenditure;
    }

    /// <summary>
    /// Shared validation for the fields <see cref="Register"/> and <see cref="Update"/> both take,
    /// returning the three required strings once confirmed non-blank so callers keep the compiler's
    /// non-null flow. Each entry point still applies its own remaining rules — <see cref="Email"/>
    /// on register, and the <see cref="Money"/> value — in its own order.
    /// </summary>
    private static ErrorOr<(string MainClass, string SubClass, string PaymentMethod)> ValidateCoreFields(
        string? mainClass, string? subClass, string? paymentMethod,
        short depositMonth, short depositDay, decimal amount, string? content, string? note)
    {
        if (string.IsNullOrWhiteSpace(mainClass))
            return LocalizableError.Validation("FixedExpenditure.MainClassEmpty", "Main expense category cannot be empty.");

        if (string.IsNullOrWhiteSpace(subClass))
            return LocalizableError.Validation("FixedExpenditure.SubClassEmpty", "Sub expense category cannot be empty.");

        if (string.IsNullOrWhiteSpace(paymentMethod))
            return LocalizableError.Validation("FixedExpenditure.PaymentMethodEmpty", "Payment method cannot be empty.");

        if (depositMonth is < 1 or > 12)
            return LocalizableError.Validation("FixedExpenditure.InvalidDepositMonth", "Deposit month must be between 1 and 12.");

        if (!FixedSchedulePolicy.IsValidDepositDate(depositMonth, depositDay))
            return LocalizableError.Validation("FixedExpenditure.InvalidDepositDay", "Deposit day is not valid for the selected month.");

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

    /// <summary>Sets the "always notify" (<see cref="Unpunctuality"/>) flag.</summary>
    public void MarkUnpunctual(DateTime utcNow)
    {
        Unpunctuality = true;
        Updated = utcNow;
    }

    /// <summary>Clears the "always notify" (<see cref="Unpunctuality"/>) flag, returning to date-based notice.</summary>
    public void ClearUnpunctuality(DateTime utcNow)
    {
        Unpunctuality = false;
        Updated = utcNow;
    }

    /// <summary>Updates all editable fields.</summary>
    public ErrorOr<Success> Update(
        string? mainClass,
        string? subClass,
        string? content,
        decimal amount,
        string? currency,
        string? paymentMethod,
        string? myDepositAsset,
        short depositMonth,
        short depositDay,
        DateTime maturityDate,
        string? note,
        DateTime utcNow)
    {
        var coreResult = ValidateCoreFields(mainClass, subClass, paymentMethod, depositMonth, depositDay, amount, content, note);
        if (coreResult.IsError) return coreResult.Errors;

        var moneyResult = Money.Create(amount, currency);
        if (moneyResult.IsError) return moneyResult.Errors;

        // Reject moving the maturity date into the past, but let an edit that leaves an already
        // past date untouched through, so an expired schedule's other fields stay editable.
        if (maturityDate.Date != MaturityDate.Date
            && !FixedSchedulePolicy.IsAcceptableMaturityDate(maturityDate, utcNow))
            return LocalizableError.Validation("FixedExpenditure.MaturityDateInPast",
                "The maturity date cannot be earlier than the current date.");

        var (validMainClass, validSubClass, validPaymentMethod) = coreResult.Value;
        MainClass = validMainClass;
        SubClass = validSubClass;
        Content = content;
        Amount = moneyResult.Value;
        PaymentMethod = validPaymentMethod;
        MyDepositAsset = myDepositAsset;
        DepositMonth = depositMonth;
        DepositDay = depositDay;
        MaturityDate = maturityDate;
        Note = note;
        Updated = utcNow;
        return Result.Success;
    }
}
