namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Result returned when validating a password-reset token before allowing the user to set a new password.
/// </summary>
public class ResetPasswordValidationResult
{
    /// <summary>True when validation failed (invalid/expired token).</summary>
    public bool FailToReset { get; init; }

    /// <summary>The validated reset token to pass back during the actual password change.</summary>
    public string? ResetPasswordToken { get; init; }
}
