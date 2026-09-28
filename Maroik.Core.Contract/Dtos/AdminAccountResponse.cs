// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// <see cref="AccountResponse"/> plus the three secret-bearing columns
/// (<see cref="HashedPassword"/>, <see cref="RegistrationToken"/>, <see cref="ResetPasswordToken"/>).
/// Returned <b>only</b> by the admin account-management service (grid / search / Excel export),
/// which is why these fields live on a distinct type: the logged-in user's own session payload is an
/// <see cref="AccountResponse"/> and therefore <i>cannot</i> carry a password hash, even by accident.
/// </summary>
public sealed class AdminAccountResponse : AccountResponse
{
    /// <summary>BCrypt password hash. Admin-only.</summary>
    public string? HashedPassword { get; set; }

    /// <summary>One-time token used to confirm the registration e-mail. Admin-only.</summary>
    public string? RegistrationToken { get; set; }

    /// <summary>One-time token used to authorize a password-reset request. Admin-only.</summary>
    public string? ResetPasswordToken { get; set; }
}
