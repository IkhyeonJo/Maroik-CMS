using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Session;
using Microsoft.Extensions.Options;

namespace Maroik.Website.Extensions;

/// <summary>
/// Extension methods for regenerating the current request's session identity.
/// </summary>
public static class HttpContextSessionExtensions
{
    extension(HttpContext context)
    {
        /// <summary>
        /// Session fixation mitigation: mints a brand new, unguessable session id instead of
        /// elevating whatever id the current request arrived with. HttpContext.Session.Clear()
        /// alone does NOT do this - ASP.NET Core's session middleware only issues a new Set-Cookie
        /// when the incoming cookie was missing/invalid, so an attacker-fixated (but still valid)
        /// cookie would otherwise be reused as-is across authentication. Call this before writing
        /// anything to Session on endpoints that establish or elevate an authenticated session
        /// (e.g. login).
        /// </summary>
        public void RegenerateSession()
        {
            // Pull the session plumbing straight from DI instead of requiring callers to inject and
            // pass it in - keeps this a one-line call site with no constructor changes needed.
            var sessionStore = context.RequestServices.GetRequiredService<ISessionStore>();
            var sessionOptions = context.RequestServices.GetRequiredService<IOptions<SessionOptions>>().Value;
            // Same purpose string SessionMiddleware itself uses internally to (un)protect the cookie,
            // so the cookie we mint below is decrypted correctly by the framework on later requests.
            var sessionCookieProtector = context.RequestServices.GetRequiredService<IDataProtectionProvider>().CreateProtector(nameof(SessionMiddleware));

            context.Session.Clear(); // drop whatever data lived under the old (possibly attacker-fixated) id

            // A fresh, unpredictable session id - Guid.NewGuid() uses a CSPRNG on modern .NET, and its
            // 36-char hyphenated format matches exactly what SessionMiddleware expects to find in the cookie.
            string newSessionKey = Guid.NewGuid().ToString();

            // Swap in a brand-new ISession under the new id. This replaces the ISessionFeature the
            // session middleware attached at the start of the request, so every Session.* call made
            // for the rest of this request (including the caller's own Session.Set right after this
            // method returns) operates on the new session, not the old one.
            context.Features.Set<ISessionFeature>(new SessionFeature
            {
                Session = sessionStore.Create(newSessionKey, sessionOptions.IdleTimeout, sessionOptions.IOTimeout, () => true, isNewSessionKey: true)
            });

            // Manually issue the Set-Cookie header for the new session id. This step is required because
            // ASP.NET Core's session middleware only writes a Set-Cookie when the incoming request had no
            // valid session cookie; since our request DID have one (that's the whole fixation problem),
            // the middleware would otherwise never send the browser this new id.
            string cookieValue = Convert.ToBase64String(sessionCookieProtector.Protect(System.Text.Encoding.UTF8.GetBytes(newSessionKey))).TrimEnd('=');
            context.Response.Cookies.Append(sessionOptions.Cookie.Name!, cookieValue, sessionOptions.Cookie.Build(context));
        }
    }
}
