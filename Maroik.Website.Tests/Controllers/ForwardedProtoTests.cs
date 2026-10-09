using System.Net;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// In production TLS ends at Cloudflare and the request reaches Kestrel as plain HTTP through the Cloudflare Tunnel
/// (<c>cloudflared</c> on the private Docker network), which states the visitor's scheme in <c>X-Forwarded-Proto</c>.
/// The app takes that scheme from a private-network peer only, so HSTS is still sent and the www redirect stays on
/// https; the same header from any other peer is ignored. The requests are sent straight into the test server so the
/// peer address can be set.
/// </summary>
[Collection("Website Integration")]
public class ForwardedProtoTests(MaroikWebApplicationFactory factory)
{
    /// <summary>Sends a plain-HTTP GET for <paramref name="host"/> from <paramref name="peer"/> carrying <c>X-Forwarded-Proto: https</c>.</summary>
    private Task<HttpContext> SendForwardedHttpsAsync(string host, string peer) =>
        factory.Server.SendAsync(context =>
        {
            context.Request.Method = HttpMethods.Get;
            context.Request.Scheme = "http";
            context.Request.Host = new HostString(host);
            context.Request.Path = "/health";
            context.Request.Headers["X-Forwarded-Proto"] = "https";
            context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
        }, TestContext.Current.CancellationToken);

    /// <summary>A request the tunnel forwarded from https gets the HSTS header, which is only ever sent over https.</summary>
    [Theory]
    [InlineData("172.18.0.5")]   // Docker bridge network
    [InlineData("10.0.1.20")]    // a VPC / private LAN
    [InlineData("192.168.0.10")]
    public async Task ForwardedHttps_FromAPrivateNetworkPeer_IsServedAsHttps(string peer)
    {
        HttpContext context = await SendForwardedHttpsAsync("www.localhost", peer);

        Assert.True(context.Response.Headers.ContainsKey(HeaderNames.StrictTransportSecurity));
    }

    /// <summary>A peer outside the private ranges cannot claim https: the header is ignored and no HSTS is sent.</summary>
    [Fact]
    public async Task ForwardedHttps_FromAPublicPeer_IsIgnored()
    {
        HttpContext context = await SendForwardedHttpsAsync("www.localhost", "203.0.113.7");

        Assert.False(context.Response.Headers.ContainsKey(HeaderNames.StrictTransportSecurity));
    }

    /// <summary>The apex-to-www redirect keeps the visitor's https scheme instead of sending them to http.</summary>
    [Fact]
    public async Task WwwRedirect_OfAForwardedHttpsRequest_StaysOnHttps()
    {
        HttpContext context = await factory.Server.SendAsync(c =>
        {
            c.Request.Method = HttpMethods.Get;
            c.Request.Scheme = "http";
            c.Request.Host = new HostString("maroik.test");
            c.Request.Path = "/Dashboard/AnonymousIndex";
            c.Request.Headers["X-Forwarded-Proto"] = "https";
            c.Connection.RemoteIpAddress = IPAddress.Parse("172.18.0.5");
        }, TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status307TemporaryRedirect, context.Response.StatusCode);
        Assert.Equal("https://www.maroik.test/Dashboard/AnonymousIndex", context.Response.Headers.Location.ToString());
    }
}
