using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Accounts;
using Microsoft.Extensions.Logging;

namespace Maroik.Core.Service.Services;

/// <summary>Logs and answers a sign-in that <see cref="Account.CheckSignInOnceAuthenticated"/> refused.</summary>
internal static class SignInRefusalResponder
{
    /// <summary>
    /// Logs the refusal for <paramref name="email"/> at <c>Warning</c> (a security event) and returns its answer. A
    /// value outside <see cref="SignInRefusal"/> throws.
    /// </summary>
    internal static LoginResult Refuse(ILogger logger, SignInRefusal refusal, string email)
    {
        switch (refusal)
        {
            case SignInRefusal.Deleted:
                logger.LogWarning("Login refused: deleted account {Email}", email);
                return LoginResult.Fail(ServiceErrorKeys.AccountDeleted);
            case SignInRefusal.EmailNotConfirmed:
                logger.LogWarning("Login refused: email not confirmed for {Email}", email);
                return LoginResult.Fail(ServiceErrorKeys.EmailNotConfirmed);
            case SignInRefusal.ServiceTermsNotAgreed:
                logger.LogWarning("Login refused: service terms not accepted for {Email}", email);
                return LoginResult.Fail(ServiceErrorKeys.ServiceTermsNotAgreed);
            default:
                throw new ArgumentOutOfRangeException(nameof(refusal), refusal, "Not a SignInRefusal member.");
        }
    }
}
