namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// The message keys a service puts in a failed result's <c>ErrorKey</c>. Each value is also the key of an entry in the resx pair of
/// every controller that relays it (<c>localizer[result.ErrorKey, result.ErrorArgs]</c>), so the text is the contract between the two
/// layers: rewording a value here means renaming that resx key in the same change. <c>ServiceErrorKeyResxTests</c>
/// (Maroik.Website.Tests) fails when a controller can surface a key its resx pair lacks, and
/// <c>ServiceErrorKeyArchitectureTests</c> (Maroik.Core.Service.Tests) fails when a service writes a key as a literal instead of
/// using one of these constants.
/// </summary>
/// <remarks>A value with <c>{0}</c> is a composite-format template; the service passes the value through <c>ErrorArgs</c>.</remarks>
public static class ServiceErrorKeys
{
    /// <summary>An unexpected / infrastructure failure the user cannot fix by changing the input.</summary>
    public const string TemporaryError = "A temporary error occurred. Please try again later.";
    /// <summary>The input does not describe anything the caller may act on.</summary>
    public const string InputInvalid = "Input is invalid";

    // --- Account: login, registration, password reset
    /// <summary>Wrong password or no such account — deliberately the same reply for both.</summary>
    public const string EmailOrPasswordWrong = "Email or Password is wrong";
    /// <summary>The account is locked after too many failed logins (or by an administrator).</summary>
    public const string AccountLocked = "Your Account is Locked, Please reset your password by clicking Forgot password Button";
    /// <summary>The account is soft-deleted.</summary>
    public const string AccountDeleted = "Your Account is Deleted. Please contact the administrator.";
    /// <summary>The registration mail was never confirmed.</summary>
    public const string EmailNotConfirmed = "Email verification was not completed. Please try sign up again.";
    /// <summary>The service terms were never accepted.</summary>
    public const string ServiceTermsNotAgreed = "Agreed Service Terms was not checked. Please try sign up again and login again.";
    /// <summary>Template: the nickname <c>{0}</c> is taken.</summary>
    public const string NicknameExists = "'{0}' is a Nickname that already exists. Please enter another Nickname.";
    /// <summary>Template: the email <c>{0}</c> is taken.</summary>
    public const string EmailExists = "'{0}' is an Email that already exists.";
    /// <summary>A registration write failed.</summary>
    public const string RegistrationFailed = "Error occurred while processing about account registration";
    /// <summary>An account-status write failed.</summary>
    public const string AccountStatusFailed = "Error occurred while processing about account status";
    /// <summary>The confirmation mail could not be queued.</summary>
    public const string AuthenticationMailFailed = "Error occurred while processing about sending account authentication mail";
    /// <summary>The confirmation mail could not be re-sent.</summary>
    public const string ResendEmailFailed = "Failed to resend email";
    /// <summary>A password-reset write failed.</summary>
    public const string ResetPasswordFailed = "Error occurred while processing about reset password";

    // --- Management / profile
    /// <summary>An administrator tried to create an account that already exists.</summary>
    public const string AccountAlreadyCreated = "This account has already been created.";
    /// <summary>An administrator's update named an unknown email address.</summary>
    public const string EmailAddressWrong = "Email address is wrong";
    /// <summary>An administrator's delete named an unknown email address.</summary>
    public const string AccountNotFoundByEmail = "Fail to find the account by given email address";
    /// <summary>The current password given to a self-service password change is wrong.</summary>
    public const string InvalidPassword = "Invalid password. Please check again.";

    // --- Board
    /// <summary>The post does not exist.</summary>
    public const string PostNotFound = "The post could not be found.";
    /// <summary>The caller may not delete the post or comment.</summary>
    public const string NoPermissionToDelete = "You do not have permission to delete.";
    /// <summary>The caller may not comment on the post.</summary>
    public const string NoPermissionToWriteComment = "You do not have permission to write a comment.";
    /// <summary>The attachment could not be stored.</summary>
    public const string AttachmentUploadFailed = "Failed to upload the attached file.";

    // --- Attachments (board and calendar)
    /// <summary>The attached file does not exist.</summary>
    public const string AttachedFileNotFound = "The attached file could not be found.";
    /// <summary>An attachment is not a zip file.</summary>
    public const string OnlyZipAllowed = "Only zip extension allowed.";
    /// <summary>Template: an attachment is larger than <c>{0}</c> MB.</summary>
    public const string FileTooLarge = "File Size must be smaller than {0}MB.";
    /// <summary>An inline (Summernote) image is not a valid image.</summary>
    public const string InvalidImageFile = "Invalid image file.";

    // --- Calendar
    /// <summary>A calendar of the same name already exists.</summary>
    public const string CalendarExists = "The calendar already exists.";
    /// <summary>The calendar does not exist (edit / delete).</summary>
    public const string CalendarNotFound = "The calendar could not be found.";
    /// <summary>The calendar does not exist (event create / update).</summary>
    public const string CalendarDoesNotExist = "The calendar does not exists.";
    /// <summary>The account has no calendar at all.</summary>
    public const string NoCalendarExists = "No calendar exists.";
    /// <summary>The calendar event does not exist.</summary>
    public const string CalendarEventNotFound = "The calendar event could not be found.";

    // --- Account book
    /// <summary>An asset of the same name already exists.</summary>
    public const string AssetExists = "The asset already exists.";
    /// <summary>The asset's category is not one of the allowed values.</summary>
    public const string AssetItemInvalid = "Asset category (item) is not a recognised value.";
    /// <summary>The asset does not exist (asset edit / delete).</summary>
    public const string AssetNotFoundByProductName = "Fail to find the asset by given product name";
    /// <summary>The asset an income / expenditure refers to does not exist.</summary>
    public const string SelectedAssetNotFound = "The selected asset could not be found.";
    /// <summary>The asset an income / expenditure refers to is soft-deleted.</summary>
    public const string AssetAlreadyDeleted = "Actions cannot be executed with assets that have already been deleted.";
    /// <summary>A transfer's payment asset and deposit asset are the same.</summary>
    public const string SameAsset = "The PaymentMethod and MyDepositAsset value cannot be the same.";
    /// <summary>A transfer's payment asset and deposit asset carry different currency labels.</summary>
    public const string CurrencyMismatch = "PaymentMethod MonetaryUnit must be same as MyDepositAsset MonetaryUnit.";
    /// <summary>The income does not exist.</summary>
    public const string IncomeNotFound = "The income record could not be found.";
    /// <summary>The expenditure does not exist.</summary>
    public const string ExpenditureNotFound = "The expenditure record could not be found.";
    /// <summary>The fixed income does not exist.</summary>
    public const string FixedIncomeNotFound = "The fixed-income record could not be found.";
    /// <summary>The fixed expenditure does not exist.</summary>
    public const string FixedExpenditureNotFound = "The fixed-expenditure record could not be found.";

    /// <summary>
    /// Keys that are not resx keys: the controller that receives one compares <c>ErrorKey</c> against it and chooses its own
    /// response (another view, or a message of its own). Shared here so the service and the controller cannot drift apart.
    /// </summary>
    public static class Signals
    {
        /// <summary>The reset token is unknown, expired, used or held by an account that cannot reset — show the "link invalid" page.</summary>
        public const string ResetPasswordInvalid = "reset-password-invalid";
        /// <summary>The avatar is not a valid JPEG / PNG.</summary>
        public const string InvalidImage = "invalid-image";
        /// <summary>The avatar is an SVG.</summary>
        public const string SvgNotAllowed = "svg-not-allowed";
        /// <summary>The virus scanner flagged the avatar.</summary>
        public const string VirusDetected = "virus-detected";
        /// <summary>The virus scanner could not be reached.</summary>
        public const string ScanUnavailable = "scan-unavailable";
    }
}
