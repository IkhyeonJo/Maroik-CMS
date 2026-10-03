using System.Security.Cryptography;
using System.Text.Json;
using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Account;
using Maroik.Website.Contracts;
using Microsoft.AspNetCore.DataProtection;

namespace Maroik.Website.Services;

/// <summary>
/// <see cref="ITrustedDeviceCookie"/> over ASP.NET Core Data Protection (the same key ring as the session and
/// antiforgery cookies), so a browser cannot forge or edit the claim.
/// </summary>
public sealed class TrustedDeviceCookie(
    IDataProtectionProvider dataProtection,
    TimeProvider timeProvider,
    ILogger<TrustedDeviceCookie> logger) : ITrustedDeviceCookie
{
    /// <summary>Name of the cookie (the <c>__Secure-</c> prefix makes browsers refuse it over plain HTTP).</summary>
    public const string CookieName = "__Secure-Maroik.TrustedDevice";

    /// <summary>Data Protection purpose string: a protector for anything else cannot read or forge this cookie.</summary>
    private const string Purpose = "Maroik.Website.TrustedDeviceCookie.v1";

    /// <summary>The protector for <see cref="Purpose"/>.</summary>
    private readonly IDataProtector _protector = dataProtection.CreateProtector(Purpose);

    /// <summary>What the cookie holds, before protection: account e-mail, device stamp, issue time (UTC).</summary>
    private sealed record Payload(string E, string S, DateTime T);

    /// <inheritdoc />
    public TrustedDeviceClaim? Read(HttpRequest request)
    {
        if (!request.Cookies.TryGetValue(CookieName, out string? value) || string.IsNullOrEmpty(value))
            return null;

        try
        {
            Payload? payload = JsonSerializer.Deserialize<Payload>(_protector.Unprotect(value));
            return payload == null ? null : new TrustedDeviceClaim(payload.E, payload.S, DateTime.SpecifyKind(payload.T, DateTimeKind.Utc));
        }
        catch (Exception e) when (e is CryptographicException or JsonException)
        {
            // Not issued by this key ring (edited, made up, or from keys that are gone): the browser is simply not a
            // trusted device. Worth a Warning, as an edited credential cookie looks hostile; the value is not logged.
            logger.LogWarning(e, "Ignored a trusted-device cookie this server did not issue");
            return null;
        }
    }

    /// <inheritdoc />
    public void Issue(HttpResponse response, string email, string deviceStamp)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        string value = _protector.Protect(JsonSerializer.Serialize(new Payload(email, deviceStamp, now.UtcDateTime)));
        response.Cookies.Append(CookieName, value, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            IsEssential = true,
            Expires = now + TrustedDevicePolicy.Lifetime
        });
    }
}
