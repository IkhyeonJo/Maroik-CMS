using ErrorOr;
using Maroik.Core.Domain.Errors;
using Maroik.Core.Domain.Primitives;

namespace Maroik.Core.Domain.ValueObjects;

/// <summary>
/// A currency label: trimmed and upper-cased, at most <see cref="MaxLength"/> characters. Normally an
/// ISO 4217 code ("KRW", "USD"), but any label is accepted (e.g. "원"), and an asset's label may be
/// changed at any time. Persisted as its <see cref="Value"/> string.
/// </summary>
public sealed class CurrencyCode : ValueObject
{
    /// <summary>Longest code the persisted <c>MonetaryUnit</c> / <c>DefaultMonetaryUnit</c> columns (<c>varchar(45)</c>) accept.</summary>
    private const int MaxLength = 45;

    /// <summary>The normalized (trimmed, upper-case) code.</summary>
    public string Value { get; }

    /// <summary>Wraps an already-normalized code; reached only through the factories.</summary>
    private CurrencyCode(string value) => Value = value;

    /// <summary>Re-creates a code read from trusted storage, normalized but not validated.</summary>
    internal static CurrencyCode FromTrustedSource(string value) => new(value.ToUpperInvariant());

    /// <summary>
    /// Validates and normalizes <paramref name="value"/>: an empty label, or one longer than the
    /// persisted column allows, is a validation error (not a raw database write failure).
    /// </summary>
    public static ErrorOr<CurrencyCode> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return DomainError.Validation("Money.CurrencyEmpty", "Currency code cannot be empty.");

        string trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
            return DomainError.Validation("Money.CurrencyTooLong", "Currency code must be {0} characters or fewer.", MaxLength);

        return new CurrencyCode(trimmed.ToUpperInvariant());
    }

    /// <inheritdoc/>
    protected override IEnumerable<object?> GetAtomicValues() { yield return Value; }

    /// <inheritdoc/>
    public override string ToString() => Value;
}
