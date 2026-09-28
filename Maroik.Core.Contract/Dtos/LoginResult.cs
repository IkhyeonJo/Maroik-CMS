// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable MemberCanBePrivate.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Result returned by the login operation.
/// Use the static factory methods <see cref="Ok"/> and <see cref="Fail"/> to create instances.
/// </summary>
public class LoginResult
{
    /// <summary>True when login succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Localization key describing the error (empty string on success).</summary>
    public string ErrorKey { get; init; } = "";

    /// <summary>The authenticated account data (null on failure).</summary>
    public AccountResponse? Account { get; init; }

    /// <summary>Creates a successful login result carrying the account data.</summary>
    public static LoginResult Ok(AccountResponse account) => new() { Success = true, Account = account };

    /// <summary>Creates a failed login result with the given error key.</summary>
    public static LoginResult Fail(string errorKey) => new() { Success = false, ErrorKey = errorKey };
}
