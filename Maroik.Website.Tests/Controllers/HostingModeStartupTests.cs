using System.Net;
using Maroik.Website.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Net.Http.Headers;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// The website started in each hosting mode (<c>ServerSetting:HostingMode</c>, set by Deploy.sh): without a domain it serves plain HTTP —
/// no redirect to https, no HSTS, cookies a browser accepts over HTTP — and ignores a forwarded scheme; behind port forwarding (Caddy)
/// it behaves as behind the tunnel. The domain modes' details are pinned by <see cref="ForwardedProtoTests"/>.
/// </summary>
[Collection("Website Integration")]
public class HostingModeStartupTests(MaroikWebApplicationFactory factory)
{
    /// <summary>A host started in <paramref name="mode"/> with <paramref name="domain"/>.</summary>
    private WebApplicationFactory<Program> Host(string mode, string domain) =>
        factory.WithWebHostBuilder(b => b.UseSetting("ServerSetting:HostingMode", mode).UseSetting("ServerSetting:DomainName", domain));

    /// <summary>Sends a plain-HTTP GET (from a private-network peer claiming https when <paramref name="forwardedHttps"/>).</summary>
    private static Task<HttpContext> GetAsync(WebApplicationFactory<Program> host, string path, bool forwardedHttps = false) =>
        host.Server.SendAsync(context =>
        {
            context.Request.Method = HttpMethods.Get;
            context.Request.Scheme = "http";
            context.Request.Host = new HostString("192.168.0.10:8080");
            context.Request.Path = path;
            if (forwardedHttps) context.Request.Headers["X-Forwarded-Proto"] = "https";
            context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.0.20");
        }, TestContext.Current.CancellationToken);

    /// <summary>Without a domain the login page is served over plain HTTP: no redirect to https or to www, no HSTS.</summary>
    [Fact]
    public async Task NoDomain_ServesPlainHttp()
    {
        using var host = Host("NoDomain", "http://192.168.0.10:8080/");

        HttpContext context = await GetAsync(host, "/Account/Login");

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.False(context.Response.Headers.ContainsKey(HeaderNames.StrictTransportSecurity));
    }

    /// <summary>Without a domain the antiforgery cookie has no "__Secure-" prefix, no Secure flag and no domain, so a browser keeps it over HTTP.</summary>
    [Fact]
    public async Task NoDomain_SetsCookiesABrowserKeepsOverHttp()
    {
        using var host = Host("NoDomain", "http://192.168.0.10:8080/");

        HttpContext context = await GetAsync(host, "/Account/Login");

        string cookie = Assert.Single(context.Response.Headers.SetCookie, c => c!.Contains("Antiforgery", StringComparison.Ordinal))!;
        Assert.StartsWith(".AspNetCore.Antiforgery", cookie, StringComparison.Ordinal);
        Assert.DoesNotContain("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Without a domain there is no proxy: a forwarded https from any peer is ignored.</summary>
    [Fact]
    public async Task NoDomain_IgnoresAForwardedScheme()
    {
        using var host = Host("NoDomain", "http://192.168.0.10:8080/");

        HttpContext context = await GetAsync(host, "/health", forwardedHttps: true);

        Assert.False(context.Response.Headers.ContainsKey(HeaderNames.StrictTransportSecurity));
    }

    /// <summary>Behind port forwarding, Caddy (a private-network peer) states https: HSTS is sent and the apex is redirected to https://www, as behind the tunnel.</summary>
    [Fact]
    public async Task PortForwarding_BehavesAsTheTunnel()
    {
        using var host = Host("PortForwarding", "https://www.maroik.test/");

        HttpContext context = await host.Server.SendAsync(c =>
        {
            c.Request.Method = HttpMethods.Get;
            c.Request.Scheme = "http";
            c.Request.Host = new HostString("maroik.test");
            c.Request.Path = "/Dashboard/AnonymousIndex";
            c.Request.Headers["X-Forwarded-Proto"] = "https";
            c.Connection.RemoteIpAddress = IPAddress.Parse("172.18.0.9");
        }, TestContext.Current.CancellationToken);

        Assert.True(context.Response.Headers.ContainsKey(HeaderNames.StrictTransportSecurity));
        Assert.StartsWith("https://www.maroik.test/", context.Response.Headers.Location.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Without a domain no www redirect runs either: an apex-looking host is served as it is.</summary>
    [Fact]
    public async Task NoDomain_DoesNotRedirectToWww()
    {
        using var host = Host("NoDomain", "http://maroik.test/");

        HttpContext context = await host.Server.SendAsync(c =>
        {
            c.Request.Method = HttpMethods.Get;
            c.Request.Scheme = "http";
            c.Request.Host = new HostString("maroik.test");
            c.Request.Path = "/Account/Login";
        }, TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    /// <summary>An unknown hosting mode stops the start-up.</summary>
    [Fact]
    public void AnUnknownMode_StopsTheStartUp()
    {
        using var host = Host("Ngrok", "https://www.localhost/");

        Exception failure = Assert.ThrowsAny<Exception>(() => host.CreateClient().Dispose());

        Assert.Contains("HostingMode", (failure.InnerException ?? failure).Message, StringComparison.Ordinal);
    }
}
