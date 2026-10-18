using System.Text.RegularExpressions;
using Maroik.Core.PostgreSQL.Contexts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Maroik.Website.Tests.Fixtures;

/// <summary>Seeds an account and drives the real <c>/Account/Login</c> flow to produce an <see cref="AuthenticatedSession"/>.</summary>
public static class AuthenticatedSessionHelper
{
    /// <summary>Name of the ASP.NET Core session cookie the site issues.</summary>
    internal const string SessionCookieName = "__Secure-.AspNetCore.Session";
    /// <summary>Name prefix of the antiforgery cookie the site issues.</summary>
    internal const string AntiforgeryCookieName = "__Secure-.AspNetCore.Antiforgery.";

    /// <summary>
    /// Inserts an account directly into the database (bypassing registration/email-confirmation)
    /// and logs in as it via a real HTTP round-trip, returning the resulting session.
    /// </summary>
    public static async Task<AuthenticatedSession> LoginAsync(
        WebApplicationFactory<Program> factory, HttpClient client, string email, string password, string role, CancellationToken ct)
    {
        // Account_Nickname_unique is a real, enforced constraint (Postgres), so every seeded
        // account needs a distinct nickname — derive it from the (already-unique) email rather
        // than a fixed placeholder.
        string nickname = "TestUser_" + email.Replace("@", "_", StringComparison.Ordinal).Replace(".", "_", StringComparison.Ordinal);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (!db.Accounts.Any(a => a.Email == email))
            {
                db.Accounts.Add(new Maroik.Core.PostgreSQL.Models.Account
                {
                    Email = email,
                    HashedPassword = TestPasswords.Hash(password),
                    Nickname = nickname,
                    AvatarImagePath = "/upload/Management/Profile/default-avatar.jpg",
                    Role = role,
                    TimeZoneIanaId = "UTC",
                    Locked = false,
                    LoginAttempt = 0,
                    EmailConfirmed = true,
                    AgreedServiceTerms = true,
                    Deleted = false,
                    Created = DateTime.UtcNow,
                    Updated = DateTime.UtcNow
                });
                await db.SaveChangesAsync(ct);
            }
        }

        (string loginToken, string loginAntiforgeryCookie) = await GetLoginPageAntiforgeryAsync(client, ct);

        using var postRequest = new HttpRequestMessage(HttpMethod.Post, "/Account/Login");
        postRequest.Headers.Add("Cookie", $"{AntiforgeryCookieName}={loginAntiforgeryCookie}");
        postRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = loginToken
        });
        var postResponse = await client.SendAsync(postRequest, ct);
        if (postResponse.StatusCode != System.Net.HttpStatusCode.Redirect)
        {
            string body = await postResponse.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Login failed for {email} ({postResponse.StatusCode}):\n{body}");
        }

        string sessionCookie = ExtractCookieValue(postResponse, SessionCookieName)
            ?? throw new InvalidOperationException("Login succeeded but issued no session cookie.");
        // The antiforgery cookie/token pair is independent of the session cookie (it is not tied to
        // authentication state), and login does not reissue it, so the pre-login pair captured from
        // the Login GET above remains valid for authenticated requests against role-gated endpoints.
        // (A logged-in GET request can't be used to mint a "fresher" token instead: the seeded test
        // menu only contains Anonymous-role categories, so any authenticated GET redirects via
        // AuthorizationFilter's menu-match fallback rather than rendering a page.)
        string antiforgeryCookie = ExtractCookieValue(postResponse, AntiforgeryCookieName) ?? loginAntiforgeryCookie;

        return new AuthenticatedSession(sessionCookie, antiforgeryCookie, loginToken, nickname);
    }

    /// <summary>
    /// An anonymous visitor's antiforgery cookie/token pair (from the Login page) with no signed-in
    /// session, for exercising the POST actions the menu grants to the Anonymous role.
    /// </summary>
    public static async Task<AuthenticatedSession> AnonymousAsync(HttpClient client, CancellationToken ct)
    {
        (string token, string antiforgeryCookie) = await GetLoginPageAntiforgeryAsync(client, ct);
        return new AuthenticatedSession("", antiforgeryCookie, token, "");
    }

    /// <summary>
    /// GETs <c>/Account/Login</c> and returns its antiforgery token / cookie pair. Anything but a rendered page (a redirect,
    /// an error page) fails with the status, the redirect target and the start of the body, so a test that cannot get a
    /// token says why instead of only that the token is missing.
    /// </summary>
    private static async Task<(string Token, string Cookie)> GetLoginPageAntiforgeryAsync(HttpClient client, CancellationToken ct)
    {
        using var getRequest = new HttpRequestMessage(HttpMethod.Get, "/Account/Login");
        using HttpResponseMessage getResponse = await client.SendAsync(getRequest, ct);
        string html = await getResponse.Content.ReadAsStringAsync(ct);
        if (getResponse.StatusCode != System.Net.HttpStatusCode.OK)
            throw new InvalidOperationException(
                $"GET /Account/Login answered {(int)getResponse.StatusCode} {getResponse.StatusCode} " +
                $"(Location: {getResponse.Headers.Location?.ToString() ?? "none"}):\n{html[..Math.Min(html.Length, 2000)]}");

        string token = ExtractAntiForgeryToken(html);
        string cookie = ExtractCookieValue(getResponse, AntiforgeryCookieName)
            ?? throw new InvalidOperationException("No antiforgery cookie on the Login GET response.");
        return (token, cookie);
    }

    /// <summary>The value of <paramref name="cookieName"/> from the response's <c>Set-Cookie</c> headers, or <see langword="null"/>.</summary>
    internal static string? ExtractCookieValue(HttpResponseMessage response, string cookieName)
    {
        return !response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders) ? null : (from header in setCookieHeaders select header.Split(';', 2)[0] into namePart let eq = namePart.IndexOf('=', StringComparison.Ordinal) where eq > 0 where namePart[..eq] == cookieName select namePart[(eq + 1)..]).FirstOrDefault();
    }

    /// <summary>The hidden <c>__RequestVerificationToken</c> value in <paramref name="html"/>; throws if there is none.</summary>
    internal static string ExtractAntiForgeryToken(string html)
    {
        Match match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        return !match.Success ? throw new InvalidOperationException("Could not find __RequestVerificationToken in the response HTML.") : match.Groups[1].Value;
    }
}
