using Maroik.Website.HostingProfiles;
using Microsoft.AspNetCore.Http;

namespace Maroik.Website.Tests.HostingProfiles;

/// <summary>
/// Unit tests for <see cref="HostingProfile"/>: how the site is reached (Cloudflare Tunnel, port forwarding behind Caddy, or no domain
/// over plain HTTP) decides HTTPS, the cookies, the trusted forwarded scheme and the cookie domain.
/// </summary>
public class HostingProfileTests
{
    /// <summary>Behind Cloudflare or Caddy, HTTPS ends at the edge: HSTS / redirects on, Secure "__Secure-" cookies, the proxy's scheme trusted.</summary>
    [Theory]
    [InlineData("Tunnel", HostingMode.Tunnel)]
    [InlineData("portforwarding", HostingMode.PortForwarding)]
    public void ADomainMode_ServesHttpsAtTheEdge(string mode, HostingMode expected)
    {
        HostingProfile profile = HostingProfile.Create(mode, "https://www.maroik.com/");

        Assert.Equal(expected, profile.Mode);
        Assert.True(profile.UsesHttps);
        Assert.True(profile.TrustsForwardedScheme);
        Assert.Equal("__Secure-", profile.CookiePrefix);
        Assert.Equal(CookieSecurePolicy.Always, profile.CookieSecurePolicy);
        Assert.Equal("www.maroik.com", profile.CookieDomain);
    }

    /// <summary>Without a domain the site is plain HTTP: no HTTPS, cookies without Secure or the "__Secure-" prefix (refused over HTTP), no cookie domain, no proxy.</summary>
    [Fact]
    public void NoDomain_ServesPlainHttp()
    {
        HostingProfile profile = HostingProfile.Create("NoDomain", "http://192.168.0.10:8080/");

        Assert.Equal(HostingMode.NoDomain, profile.Mode);
        Assert.False(profile.UsesHttps);
        Assert.False(profile.TrustsForwardedScheme);
        Assert.Equal("", profile.CookiePrefix);
        Assert.Equal(CookieSecurePolicy.SameAsRequest, profile.CookieSecurePolicy);
        Assert.Null(profile.CookieDomain);
    }

    /// <summary>Without a mode the site is the Tunnel deployment it has always been, whatever the domain setting says.</summary>
    [Theory]
    [InlineData(null, "https://www.localhost")]
    [InlineData("", "")]
    public void NoMode_IsTheTunnel(string? mode, string domain)
    {
        HostingProfile profile = HostingProfile.Create(mode, domain);

        Assert.Equal(HostingMode.Tunnel, profile.Mode);
        Assert.Equal(domain == "" ? "" : "www.localhost", profile.CookieDomain);
    }

    /// <summary>An unknown mode, or a domain mode named without a domain, refuses to start.</summary>
    [Theory]
    [InlineData("Ngrok", "https://www.maroik.com/", "HostingMode")]
    [InlineData("Tunnel", "", "DomainName")]
    [InlineData("PortForwarding", " ", "DomainName")]
    public void BadSettings_RefuseToStart(string mode, string domain, string named)
    {
        var failure = Assert.Throws<InvalidOperationException>(() => HostingProfile.Create(mode, domain));

        Assert.Contains(named, failure.Message, StringComparison.Ordinal);
    }
}
