using System.ComponentModel;

namespace Maroik.Common.Miscellaneous.Utilities
{
    public enum AccountMessage
    {
        [Description("Success")]
        Success,
        [Description("Success to reset password")]
        SuccessToResetPassword,
        [Description("Error Found")]
        ErrorFound,
        [Description("User already created, please login")]
        UserAlreadyCreated,
        [Description("User already created, please verify your given mail Id")]
        VerifyEmail,
        [Description("Invalid User, Please Create account")]
        InvalidUser,
        [Description("Mail Sent")]
        MailSent,
        [Description("Fail to mail sent")]
        FailToMailSent,
        [Description("User created, Check email, click link and verify")]
        UserCreatedVerifyEmail,
        [Description("Invalid Token")]
        InvalidToken,
        [Description("Failed to resend email")]
        FailToResendEmail,
        [Description("Email has been sent to reset password")]
        ResetPasswordMail,
        [Description("This account is locked")]
        AccountLocked
    }

    public enum Session
    {
        [Description("Account")]
        Account
    }

    public enum BoardType
    {
        [Description("FreeForum")]
        FreeForum,
        [Description("PrivateNote")]
        PrivateNote
    }

    public enum CalendarType
    {
        [Description("My")]
        My,
        [Description("Other")]
        Other
    }
}
