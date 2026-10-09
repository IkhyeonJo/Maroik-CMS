using ErrorOr;
using Maroik.Core.Domain.Errors;
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

    /// <summary>Currency code (e.g. "KRW", "USD"); see <see cref="CurrencyCode"/>.</summary>
    public CurrencyCode Currency { get; }

    /// <summary>Wraps an already-validated amount and upper-cased currency; reached only through the factories.</summary>
    private Money(decimal amount, CurrencyCode currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>
    /// Re-creates a <see cref="Money"/> from values already stored in the database.
    /// Skips validation — only call this when the source is trusted (repository layer).
    /// </summary>
    internal static Money FromTrustedSource(decimal amount, string currency) =>
        new(amount, CurrencyCode.FromTrustedSource(currency));

    /// <summary>
    /// Creates a <see cref="Money"/> value object.
    /// Returns a validation error if <paramref name="currency"/> is not a valid <see cref="CurrencyCode"/>
    /// (null, empty, or longer than the persisted column allows).
    /// </summary>
    public static ErrorOr<Money> Create(decimal amount, string? currency)
    {
        var currencyResult = CurrencyCode.Create(currency);
        if (currencyResult.IsError) return currencyResult.Errors;

        return new Money(amount, currencyResult.Value);
    }

    /// <summary>Creates a zero-balance <see cref="Money"/> with the given currency.</summary>
    public static Money Zero(CurrencyCode currency) => new(0m, currency);

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
        if (Currency != other.Currency)
            return DomainError.Validation("Money.CurrencyMismatch", "Cannot add {0} and {1}.", Currency.Value, other.Currency.Value);

        return new Money(Amount + other.Amount, Currency);
    }

    /// <summary>
    /// Returns a new <see cref="Money"/> whose amount equals this minus <paramref name="other"/>,
    /// or a validation error when the currencies differ.
    /// </summary>
    public ErrorOr<Money> Subtract(Money other)
    {
        if (Currency != other.Currency)
            return DomainError.Validation("Money.CurrencyMismatch", "Cannot subtract {0} from {1}.", other.Currency.Value, Currency.Value);

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
