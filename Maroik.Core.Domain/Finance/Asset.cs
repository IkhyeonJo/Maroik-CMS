using ErrorOr;
using Maroik.Core.Domain.Localization;
using Maroik.Core.Domain.Primitives;
using Maroik.Core.Domain.ValueObjects;

namespace Maroik.Core.Domain.Finance;

/// <summary>
/// Aggregate root representing a financial asset owned by an account
/// (e.g. a bank account, stock portfolio, cash wallet).
/// The composite identity is (<see cref="ProductName"/>, <see cref="AccountEmail"/>).
/// <para>
/// <see cref="Entity{TId}.Id"/> is captured once at construction. <see cref="Update"/> can change
/// <see cref="ProductName"/> (a rename, persisted via <c>AssetRepository.UpdateAssetWithProductNameAsync</c>
/// keyed on the original name, with <c>ON UPDATE CASCADE</c> propagating it to referencing rows),
/// which leaves this in-memory instance's <see cref="Entity{TId}.Id"/> holding the pre-rename name
/// until it is reloaded. The rename flow does exactly one load-mutate-persist per unit of work and
/// never compares <see cref="Asset"/> instances by identity, so this is currently safe; a caller
/// that put an <see cref="Asset"/> in a hash-keyed collection and looked it up again after a
/// rename would not find it.
/// </para>
/// </summary>
public sealed class Asset : AggregateRoot<(string ProductName, string AccountEmail)>
{
    /// <summary>Product/account name that identifies the asset within the owner's portfolio.</summary>
    public string ProductName { get; private set; }

    /// <summary>Email of the owning account.</summary>
    public Email AccountEmail { get; private set; }

    /// <summary>Asset category — an <see cref="AssetItemType"/> member name (e.g. "FreeDepositAndWithdrawal", "CashAsset").</summary>
    public string Item { get; private set; }

    /// <summary>Current balance of the asset.</summary>
    public Money Balance { get; private set; }

    /// <summary>UTC timestamp when the asset was registered.</summary>
    public DateTime Created { get; private set; }

    /// <summary>UTC timestamp of the most recent update.</summary>
    public DateTime Updated { get; private set; }

    /// <summary>Optional memo about the asset.</summary>
    public string? Note { get; private set; }

    /// <summary>Soft-delete flag.</summary>
    public bool Deleted { get; private set; }

    /// <summary>"Newly registered asset" constructor: stamps <see cref="Created"/>/<see cref="Updated"/>. Used by <see cref="Create"/> only.</summary>
    private Asset(
        string productName,
        Email accountEmail,
        string item,
        Money balance) : base((productName, accountEmail.Value))
    {
        ProductName = productName;
        AccountEmail = accountEmail;
        Item = item;
        Balance = balance;
        Created = DateTime.UtcNow;
        Updated = DateTime.UtcNow;
    }

    /// <summary>Reconstitution constructor: assigns every field verbatim from trusted storage with no <see cref="DateTime.UtcNow"/> side effect.</summary>
    private Asset(
        string productName, Email accountEmail, string item, Money balance,
        string? note, bool deleted, DateTime created, DateTime updated) : base((productName, accountEmail.Value))
    {
        ProductName = productName;
        AccountEmail = accountEmail;
        Item = item;
        Balance = balance;
        Note = note;
        Deleted = deleted;
        Created = created;
        Updated = updated;
    }

    // ------------------------------------------------------------------------
    // Factory / Reconstitution
    // ------------------------------------------------------------------------

    /// <summary>
    /// Rebuilds an <see cref="Asset"/> from trusted raw values from the repository layer.
    /// Bypasses validation — use only with data from the trusted repository layer.
    /// </summary>
    public static Asset Reconstitute(
        string productName,
        string accountEmail,
        string item,
        decimal amount,
        string monetaryUnit,
        string? note,
        bool deleted,
        DateTime created,
        DateTime updated)
    {
        return new Asset(
            productName,
            Email.FromTrustedSource(accountEmail),
            item,
            Money.FromTrustedSource(amount, monetaryUnit),
            note,
            deleted,
            created,
            updated);
    }

    /// <summary>
    /// Registers a new asset.
    /// </summary>
    public static ErrorOr<Asset> Create(
        string? productName,
        string? accountEmailValue,
        string? item,
        decimal amount,
        string? currency,
        string? note = null)
    {
        if (string.IsNullOrWhiteSpace(productName))
            return LocalizableError.Validation("Asset.ProductNameEmpty", "Product name cannot be empty.");

        var textResult = ValidateNameAndNote(productName, note);
        if (textResult.IsError) return textResult.Errors;

        var rangeResult = FinanceAmountPolicy.ValidateWithinRange(amount);
        if (rangeResult.IsError) return rangeResult.Errors;

        if (string.IsNullOrWhiteSpace(item))
            return LocalizableError.Validation("Asset.ItemEmpty", "Asset category (item) cannot be empty.");

        // Constrain to the known taxonomy here rather than trusting the caller: otherwise the DB
        // Asset_Item_check constraint would be the only thing rejecting anything else, which
        // surfaces as a raw DB exception instead of this validation error.
        if (!AssetItems.IsKnown(item))
            return LocalizableError.Validation("Asset.ItemInvalid", "Asset category (item) is not a recognised value.");

        var emailResult = Email.Create(accountEmailValue);
        if (emailResult.IsError) return emailResult.Errors;

        var moneyResult = Money.Create(amount, currency);
        if (moneyResult.IsError) return moneyResult.Errors;

        var asset = new Asset(productName, emailResult.Value, item, moneyResult.Value)
        {
            Note = note
        };

        return asset;
    }

    // ------------------------------------------------------------------------
    // Domain behaviours
    // ------------------------------------------------------------------------

    /// <summary>
    /// Product name and note are persisted in <c>character varying(255)</c> columns: reject an over-long
    /// value here (a clean validation error) instead of letting the write fail with a raw
    /// "value too long" (SQLSTATE 22001).
    /// </summary>
    private static ErrorOr<Success> ValidateNameAndNote(string productName, string? note)
    {
        if (productName.Length > FinanceTextPolicy.MaxTextLength)
            return LocalizableError.Validation("Asset.ProductNameTooLong", "Product name must be {0} characters or fewer.", FinanceTextPolicy.MaxTextLength);

        if (note is { Length: > FinanceTextPolicy.MaxTextLength })
            return LocalizableError.Validation("Finance.NoteTooLong", "Note must be {0} characters or fewer.", FinanceTextPolicy.MaxTextLength);

        return Result.Success;
    }

    /// <summary>Adds the given amount to the balance (e.g. on income deposit).</summary>
    public ErrorOr<Success> Deposit(Money amount)
    {
        if (amount.Currency != Balance.Currency)
            return LocalizableError.Validation("Asset.CurrencyMismatch", "Cannot deposit a different currency into this asset.");

        var addResult = Balance.Add(amount);
        if (addResult.IsError) return addResult.Errors;

        // The balance is persisted in a numeric(18,2) column: a deposit that would take it past the
        // range is a clean validation error here, not a raw numeric-overflow (SQLSTATE 22003) at save.
        var rangeResult = FinanceAmountPolicy.ValidateWithinRange(addResult.Value.Amount);
        if (rangeResult.IsError) return rangeResult.Errors;

        Balance = addResult.Value;
        Updated = DateTime.UtcNow;
        return Result.Success;
    }

    /// <summary>Subtracts the given amount from the balance (e.g. on expenditure payment).</summary>
    public ErrorOr<Success> Withdraw(Money amount)
    {
        if (amount.Currency != Balance.Currency)
            return LocalizableError.Validation("Asset.CurrencyMismatch", "Cannot withdraw a different currency from this asset.");

        var subtractResult = Balance.Subtract(amount);
        if (subtractResult.IsError) return subtractResult.Errors;

        // Same numeric(18,2) bound as Deposit, in the negative direction.
        var rangeResult = FinanceAmountPolicy.ValidateWithinRange(subtractResult.Value.Amount);
        if (rangeResult.IsError) return rangeResult.Errors;

        Balance = subtractResult.Value;
        Updated = DateTime.UtcNow;
        return Result.Success;
    }

    /// <summary>Directly sets the balance to the given value (e.g. reconciliation).</summary>
    public ErrorOr<Success> SetBalance(Money newBalance)
    {
        if (newBalance.Currency != Balance.Currency)
            return LocalizableError.Validation("Asset.CurrencyMismatch", "Cannot change the currency of an existing asset.");

        var rangeResult = FinanceAmountPolicy.ValidateWithinRange(newBalance.Amount);
        if (rangeResult.IsError) return rangeResult.Errors;

        Balance = newBalance;
        Updated = DateTime.UtcNow;
        return Result.Success;
    }

    /// <summary>
    /// Updates all editable fields of the asset, including renaming and currency changes.
    /// Used by the service layer when an admin/user modifies asset details.
    /// </summary>
    public ErrorOr<Success> Update(string productName, string item, decimal amount, string currency, string? note, bool deleted)
    {
        if (string.IsNullOrWhiteSpace(productName))
            return LocalizableError.Validation("Asset.ProductNameEmpty", "Product name cannot be empty.");

        var textResult = ValidateNameAndNote(productName, note);
        if (textResult.IsError) return textResult.Errors;

        var rangeResult = FinanceAmountPolicy.ValidateWithinRange(amount);
        if (rangeResult.IsError) return rangeResult.Errors;

        if (string.IsNullOrWhiteSpace(item))
            return LocalizableError.Validation("Asset.ItemEmpty", "Asset category cannot be empty.");

        if (!AssetItems.IsKnown(item))
            return LocalizableError.Validation("Asset.ItemInvalid", "Asset category (item) is not a recognised value.");

        var moneyResult = Money.Create(amount, currency);
        if (moneyResult.IsError) return moneyResult.Errors;

        ProductName = productName;
        Item = item;
        Balance = moneyResult.Value;
        Note = note;
        Deleted = deleted;
        Updated = DateTime.UtcNow;
        return Result.Success;
    }

    /// <summary>Updates the optional memo attached to this asset.</summary>
    public void UpdateNote(string? note)
    {
        Note = note;
        Updated = DateTime.UtcNow;
    }

    /// <summary>Marks the asset as logically deleted.</summary>
    public void SoftDelete()
    {
        Deleted = true;
        Updated = DateTime.UtcNow;
    }
}
