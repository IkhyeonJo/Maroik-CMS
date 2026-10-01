using System.Net.Mail;
using ErrorOr;
using Maroik.Core.Domain.Localization;
using Maroik.Core.Domain.Primitives;

namespace Maroik.Core.Domain.ValueObjects;

/// <summary>
/// Value object that represents a validated, normalized email address.
/// Always stored in lower-case.
/// </summary>
public sealed class Email : ValueObject
{
    /// <summary>The normalized (lower-case) email address string.</summary>
    public string Value { get; }

    /// <summary>Longest address the persisted <c>character varying(255)</c> e-mail columns accept.</summary>
    public const int MaxLength = ShortTextPolicy.MaxLength;

    /// <summary>Wraps an already-normalized address; reached only through the factories.</summary>
    private Email(string value) => Value = value;

    /// <summary>
    /// Re-creates an <see cref="Email"/> from a value already stored in the database.
    /// Skips validation — only call this when the source is trusted (repository layer).
    /// </summary>
    internal static Email FromTrustedSource(string value) => new(value.ToLowerInvariant());

    /// <summary>
    /// Creates an <see cref="Email"/> value object, validating format in the process.
    /// Returns a validation error if the value is null, empty, or not a valid email format.
    /// The stored value is the parsed, normalized address (lower-case) — not the raw input — so a
    /// display-name form ("Jane &lt;jane@x.com&gt;") or surrounding whitespace can never be persisted
    /// verbatim as the address / account key.
    /// </summary>
    public static ErrorOr<Email> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return LocalizableError.Validation("Email.Empty", "Email address cannot be empty.");

        string trimmed = value.Trim();

        // Persisted in character varying(255) columns (Account.Email and every table that references it):
        // an over-long address is a clean validation error here, not a raw "value too long" (SQLSTATE
        // 22001) from the write.
        if (trimmed.Length > MaxLength)
            return LocalizableError.Validation("Email.TooLong", "Email address must be {0} characters or fewer.", MaxLength);

        if (!TryParseAddress(trimmed, out string normalized))
            return LocalizableError.Validation("Email.Invalid", "'{0}' is not a valid email address.", value);

        return new Email(normalized);
    }

    /// <summary>
    /// The form an address typed for a lookup (login, password reset, resend) is matched in: the
    /// same trimming and lower-casing <see cref="Create"/> applies before storing, without
    /// validating it — an address that is not stored simply matches nothing.
    /// </summary>
    public static string NormalizeForLookup(string? value) => (value ?? "").Trim().ToLowerInvariant();

    /// <summary>
    /// Validates <paramref name="input"/> as a bare email address and, on success, emits its
    /// normalized (lower-case) form in <paramref name="normalized"/>. Rejects the display-name form
    /// (<c>MailAddress</c> parses "Jane &lt;jane@x.com&gt;" happily) by requiring the parsed address
    /// to equal the input.
    /// </summary>
    private static bool TryParseAddress(string input, out string normalized)
    {
        normalized = "";

        if (!MailAddress.TryCreate(input, out MailAddress? address))
            return false;

        if (!string.Equals(address.Address, input, StringComparison.OrdinalIgnoreCase))
            return false;

        string[] hostParts = address.Host.Split('.');
        if (hostParts.Length == 1)
            return false;

        if (hostParts.Any(p => p == ""))
            return false;

        if (hostParts[^1].Length < 2)
            return false;

        if (address.User.Contains(' ') || address.User.Split('.').Any(p => p == ""))
            return false;

        normalized = address.Address.ToLowerInvariant();
        return true;
    }

    /// <inheritdoc/>
    protected override IEnumerable<object?> GetAtomicValues() { yield return Value; }

    /// <inheritdoc/>
    public override string ToString() => Value;
}
