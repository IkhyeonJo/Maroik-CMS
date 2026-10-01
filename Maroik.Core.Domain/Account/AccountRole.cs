using ErrorOr;
using Maroik.Core.Domain.Localization;
using Maroik.Core.Domain.Primitives;

namespace Maroik.Core.Domain.Account;

/// <summary>
/// The role of an account: <see cref="Admin"/> or <see cref="User"/>. Unlike the menu's
/// <see cref="Role"/> strings it can never be <see cref="Role.Anonymous"/> (an anonymous visitor
/// has no account) or any other value. Persisted as its <see cref="Value"/> string.
/// </summary>
public sealed class AccountRole : ValueObject
{
    /// <summary>Full administrative access.</summary>
    public static readonly AccountRole Admin = new(Role.Admin);

    /// <summary>A standard signed-in user.</summary>
    public static readonly AccountRole User = new(Role.User);

    /// <summary>The stored role string ("Admin" or "User").</summary>
    public string Value { get; }

    /// <summary>True for the <see cref="Admin"/> role.</summary>
    public bool IsAdmin => this == Admin;

    /// <summary>Wraps an already-validated role string; reached only through the factories.</summary>
    private AccountRole(string value) => Value = value;

    /// <summary>Re-creates a role read from trusted storage without validating it.</summary>
    internal static AccountRole FromTrustedSource(string value) => new(value);

    /// <summary>Parses <paramref name="value"/>: exactly "Admin" or "User", anything else is a validation error.</summary>
    public static ErrorOr<AccountRole> Create(string? value) => value switch
    {
        Role.Admin => Admin,
        Role.User => User,
        _ => LocalizableError.Validation("Account.RoleInvalid", "Role must be either Admin or User.")
    };

    /// <inheritdoc/>
    protected override IEnumerable<object?> GetAtomicValues() { yield return Value; }

    /// <inheritdoc/>
    public override string ToString() => Value;
}
