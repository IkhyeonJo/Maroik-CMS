using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;

namespace Maroik.Website.Tests.Fixtures;

/// <summary>
/// A logged-in browser session captured from a real <c>/Account/Login</c> round-trip:
/// the authenticated session cookie plus a matching antiforgery cookie/token pair, ready
/// to be attached to subsequent authenticated requests against role-gated JSON endpoints
/// (the pattern every <c>[RequiredHttpPostAccess]</c> action requires).
/// </summary>
public sealed class AuthenticatedSession(string sessionCookieValue, string antiforgeryCookieValue, string antiforgeryToken, string nickname)
{
    /// <summary>Cookie header value combining the session and antiforgery cookies.</summary>
    public string CookieHeader =>
        $"{AuthenticatedSessionHelper.SessionCookieName}={sessionCookieValue}; {AuthenticatedSessionHelper.AntiforgeryCookieName}={antiforgeryCookieValue}";

    /// <summary>The seeded account's nickname (derived from its email — see <see cref="AuthenticatedSessionHelper.LoginAsync"/>).</summary>
    public string Nickname => nickname;

    /// <summary>
    /// Builds a POST request against a JSON <c>[FromBody]</c> endpoint, with the session
    /// cookie and antiforgery header already attached.
    /// </summary>
    public HttpRequestMessage BuildJsonPostRequest(string url, object? body = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Cookie", CookieHeader);
        request.Headers.Add("RequestVerificationToken", antiforgeryToken);
        if (body != null) request.Content = JsonContent.Create(body);
        return request;
    }

    /// <summary>
    /// Builds a POST request against a <c>[FromForm]</c>/model-bound-without-attribute endpoint
    /// (i.e. one accepting an <see cref="IFormFile"/>), with the session cookie and antiforgery
    /// header already attached.
    /// </summary>
    public HttpRequestMessage BuildFormPostRequest(string url, MultipartFormDataContent content)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Cookie", CookieHeader);
        request.Headers.Add("RequestVerificationToken", antiforgeryToken);
        request.Content = content;
        return request;
    }
}
