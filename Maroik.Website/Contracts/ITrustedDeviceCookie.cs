using Maroik.Core.Contract.Dtos;

namespace Maroik.Website.Contracts;

/// <summary>
/// Reads and writes the trusted-device cookie: a signed (Data Protection) note, issued to a browser on a successful
/// sign-in, of which account signed in there, that account's device stamp, and when. Whether it still makes the browser
/// a trusted device is decided by the account (<c>Account.TrustsDevice</c>); this only proves the cookie is ours.
/// </summary>
public interface ITrustedDeviceCookie
{
    /// <summary>The verified claim of the request's trusted-device cookie, or <see langword="null"/> when it has none or it is not ours.</summary>
    TrustedDeviceClaim? Read(HttpRequest request);

    /// <summary>Issues (or renews) the trusted-device cookie for <paramref name="email"/> and its <paramref name="deviceStamp"/>.</summary>
    void Issue(HttpResponse response, string email, string deviceStamp);
}
