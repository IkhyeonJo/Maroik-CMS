namespace Maroik.Core.Contract.Misc.Enums;

/// <summary>
/// What a value protected by <c>IDataProtectionService</c> is. Each kind is protected under its own purpose, so a
/// value handed out as one kind is never accepted as the other.
/// </summary>
public enum ProtectedValue
{
    /// <summary>A registration or password-reset token carried by a mailed link.</summary>
    MailToken = 0,

    /// <summary>
    /// The storage path of a stored file the client carries and sends back: an inline editor image's <c>alt</c>.
    /// </summary>
    StoredFilePath,
}
