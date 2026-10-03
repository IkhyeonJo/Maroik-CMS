using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Account;
using Maroik.Website.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Maroik.Website.Tests.Services;

/// <summary>
/// Unit tests for <see cref="TrustedDeviceCookie"/> over a real (ephemeral) Data Protection key ring: what it issues
/// reads back as the same claim, with the cookie attributes a credential-like cookie needs, and anything not issued by
/// this key ring reads as no claim.
/// </summary>
public class TrustedDeviceCookieTests
{
    /// <summary>The instant the clock is fixed at.</summary>
    private static readonly DateTimeOffset _now = new(2031, 3, 4, 5, 6, 7, TimeSpan.Zero);

    /// <summary>The key ring the cookie under test signs with.</summary>
    private readonly IDataProtectionProvider _keys = new EphemeralDataProtectionProvider();

    /// <summary>Captures what the cookie under test logs.</summary>
    private readonly FakeLogger<TrustedDeviceCookie> _logger = new();

    /// <summary>The cookie under test.</summary>
    private TrustedDeviceCookie CreateSut(IDataProtectionProvider? keys = null) =>
        new(keys ?? _keys, new FakeTimeProvider(_now), _logger);

    /// <summary>The <c>Set-Cookie</c> header <paramref name="sut"/> writes for user@example.com / "device-stamp".</summary>
    private static string IssuedSetCookie(TrustedDeviceCookie sut)
    {
        var context = new DefaultHttpContext();
        sut.Issue(context.Response, "user@example.com", "device-stamp");
        return Assert.Single(context.Response.Headers.SetCookie.ToArray())!;
    }

    /// <summary>A request carrying the cookie value of <paramref name="setCookie"/>.</summary>
    private static HttpRequest RequestWith(string setCookie)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = setCookie.Split(';')[0];
        return context.Request;
    }

    /// <summary>An issued cookie reads back as the claim it was issued for, dated at the clock's reading.</summary>
    [Fact]
    public void Read_ReturnsTheIssuedClaim()
    {
        TrustedDeviceCookie sut = CreateSut();

        TrustedDeviceClaim? claim = sut.Read(RequestWith(IssuedSetCookie(sut)));

        Assert.Equal(new TrustedDeviceClaim("user@example.com", "device-stamp", _now.UtcDateTime), claim);
    }

    /// <summary>The cookie is HTTPS-only, out of reach of scripts, same-site only, and lasts as long as the trust does.</summary>
    [Fact]
    public void Issue_WritesASecureHttpOnlyStrictCookie_ThatLastsTheTrustLifetime()
    {
        string setCookie = IssuedSetCookie(CreateSut());

        Assert.StartsWith(TrustedDeviceCookie.CookieName + "=", setCookie);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=" + (_now + TrustedDevicePolicy.Lifetime).ToString("R"), setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("device-stamp", setCookie);
        Assert.DoesNotContain("user@example.com", setCookie);
    }

    /// <summary>No cookie reads as no claim, and nothing is logged.</summary>
    [Fact]
    public void Read_ReturnsNull_WithoutACookie()
    {
        Assert.Null(CreateSut().Read(new DefaultHttpContext().Request));
        Assert.Empty(_logger.Collector.GetSnapshot());
    }

    /// <summary>
    /// A cookie this key ring did not issue — edited, made up, or signed with other keys — reads as no claim, and is
    /// logged as a Warning without its value.
    /// </summary>
    [Fact]
    public void Read_ReturnsNull_AndLogsAWarning_ForACookieThisKeyRingDidNotIssue()
    {
        string foreign = IssuedSetCookie(CreateSut(new EphemeralDataProtectionProvider()));
        string value = foreign.Split(';')[0];

        TrustedDeviceClaim? fromOtherKeys = CreateSut().Read(RequestWith(foreign));
        TrustedDeviceClaim? madeUp = CreateSut().Read(RequestWith(TrustedDeviceCookie.CookieName + "=not-a-protected-value"));

        Assert.Null(fromOtherKeys);
        Assert.Null(madeUp);
        Assert.Equal(2, _logger.Collector.GetSnapshot().Count(r => r.Level == LogLevel.Warning));
        Assert.All(_logger.Collector.GetSnapshot(), r => Assert.DoesNotContain(value.Split('=', 2)[1], r.Message));
    }
}
