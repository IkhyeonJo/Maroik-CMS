namespace Maroik.Core.Domain.Account;

/// <summary>
/// Why <see cref="Account.CheckSignInOnceAuthenticated"/> refuses to sign in a caller that has proven the password.
/// The domain only decides; the service answers each refusal with its own message key and audit log.
/// </summary>
public enum SignInRefusal
{
    /// <summary>The account is soft-deleted; it cannot be re-registered either.</summary>
    Deleted,

    /// <summary>The registration mail's link was never followed.</summary>
    EmailNotConfirmed,

    /// <summary>The service terms were never accepted.</summary>
    ServiceTermsNotAgreed,
}
