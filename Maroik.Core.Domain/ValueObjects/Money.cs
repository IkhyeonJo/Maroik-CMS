using ErrorOr;
using Maroik.Core.Domain.Localization;
using Maroik.Core.Domain.Primitives;

namespace Maroik.Core.Domain.ValueObjects;

/// <summary>
/// Value object representing a monetary amount with an explicit currency code (e.g. "KRW", "USD").
/// Arithmetic operations enforce currency homogeneity.
/// </summary>
public sealed class Money : ValueObject
{
    /// <summary>Numeric amount (can be negative for debits).</summary>
    public decimal Amount { get; }

    /// <summary>ISO 4217 currency code in upper-case (e.g. "KRW", "USD").</summary>
    public string Currency { get; }

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>
    /// Re-creates a <see cref="Money"/> from values already stored in the database.
    /// Skips validation — only call this when the source is trusted (repository layer).
    /// </summary>
    internal static Money FromTrustedSource(decimal amount, string currency) =>
        new(amount, currency.ToUpperInvariant());

    /// <summary>Longest currency code the persisted <c>MonetaryUnit</c> columns (<c>varchar(45)</c>) accept.</summary>
    private const int MaxCurrencyLength = 45;

    /// <summary>
    /// Creates a <see cref="Money"/> value object.
    /// Returns a validation error if <paramref name="currency"/> is null, empty, or longer than the
    /// persisted column allows (so an over-long value is a clean error, not a raw DB write failure).
    /// </summary>
    public static ErrorOr<Money> Create(decimal amount, string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
            return LocalizableError.Validation("Money.CurrencyEmpty", "Currency code cannot be empty.");

        string trimmedCurrency = currency.Trim();
        if (trimmedCurrency.Length > MaxCurrencyLength)
            return LocalizableError.Validation("Money.CurrencyTooLong", "Currency code must be {0} characters or fewer.", MaxCurrencyLength);

        return new Money(amount, trimmedCurrency.ToUpperInvariant());
    }

    /// <summary>Creates a zero-balance <see cref="Money"/> with the given currency.</summary>
    public static Money Zero(string currency) => new(0m, currency.ToUpperInvariant());

    /// <summary>
    /// Returns a new <see cref="Money"/> with the same currency as this instance but a different
    /// amount. No validation is needed since the currency is inherited from an already-valid
    /// <see cref="Money"/> rather than parsed from raw external input.
    /// </summary>
    public Money WithAmount(decimal amount) => new(amount, Currency);

    /// <summary>
    /// Returns a new <see cref="Money"/> whose amount is the sum of both operands, or a validation
    /// error when the currencies differ.
    /// </summary>
    public ErrorOr<Money> Add(Money other)
    {
        if (!Currency.Equals(other.Currency, StringComparison.OrdinalIgnoreCase))
            return LocalizableError.Validation("Money.CurrencyMismatch", "Cannot add {0} and {1}.", Currency, other.Currency);

        return new Money(Amount + other.Amount, Currency);
    }

    /// <summary>
    /// Returns a new <see cref="Money"/> whose amount equals this minus <paramref name="other"/>,
    /// or a validation error when the currencies differ.
    /// </summary>
    public ErrorOr<Money> Subtract(Money other)
    {
        if (!Currency.Equals(other.Currency, StringComparison.OrdinalIgnoreCase))
            return LocalizableError.Validation("Money.CurrencyMismatch", "Cannot subtract {0} from {1}.", other.Currency, Currency);

        return new Money(Amount - other.Amount, Currency);
    }

    /// <inheritdoc/>
    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return Amount;
        yield return Currency;
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Amount} {Currency}";
}
