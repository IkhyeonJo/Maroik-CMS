using System.ComponentModel;

namespace Maroik.Core.Domain.Account;

/// <summary>
/// Result/status messages for account operations.
/// </summary>
public enum AccountMessage
{
    /// <summary>The operation completed without errors.</summary>
    [Description("Success")]
    Success,

    /// <summary>The password-reset operation completed successfully.</summary>
    [Description("Success to reset password")]
    SuccessToResetPassword,

    /// <summary>A generic error occurred during the operation.</summary>
    [Description("Error Found")]
    ErrorFound,

    /// <summary>The email is already registered and the account is active.</summary>
    [Description("User already created, please login")]
    UserAlreadyCreated,

    /// <summary>The email is already registered but not yet confirmed.</summary>
    [Description("User already created, please verify your given mail Id")]
    VerifyEmail,

    /// <summary>No matching account was found for the supplied credentials.</summary>
    [Description("Invalid User, Please Create account")]
    InvalidUser,

    /// <summary>The confirmation or reset email was sent successfully.</summary>
    [Description("Mail Sent")]
    MailSent,

    /// <summary>An error occurred while attempting to send the email.</summary>
    [Description("Fail to mail sent")]
    FailToMailSent,

    /// <summary>A new account was created; the user must click the verification link in the confirmation email.</summary>
    [Description("User created, Check email, click link and verify")]
    UserCreatedVerifyEmail,

    /// <summary>The supplied token is invalid or has expired.</summary>
    [Description("Invalid Token")]
    InvalidToken,

    /// <summary>Resending the confirmation email failed.</summary>
    [Description("Failed to resend email")]
    FailToResendEmail,

    /// <summary>A password-reset email has been dispatched to the supplied address.</summary>
    [Description("Email has been sent to reset password")]
    ResetPasswordMail,

    /// <summary>The account is locked after too many failed login attempts.</summary>
    [Description("This account is locked")]
    AccountLocked
}
