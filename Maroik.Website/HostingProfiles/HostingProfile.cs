namespace Maroik.Website.HostingProfiles;

/// <summary>
/// What the way visitors reach the site (<see cref="HostingMode"/>) means for the pipeline. Behind Cloudflare (Tunnel) or Caddy
/// (PortForwarding) HTTPS ends at the edge: HSTS and the https / www redirects are on, the cookies are Secure with the "__Secure-"
/// prefix and the proxy's X-Forwarded-Proto is trusted from a private-network peer. Without a domain the site is plain HTTP: no
/// HSTS or redirects, cookies without Secure and without the prefix (a browser refuses a "__Secure-" cookie over HTTP), no cookie
/// domain and no proxy to trust.
/// </summary>
/// <param name="Mode">How the site is reached.</param>
/// <param name="UsesHttps">True when HTTPS ends at an edge in front of the site (HSTS, https and www redirects on).</param>
/// <param name="TrustsForwardedScheme">True when a private-network peer's X-Forwarded-Proto is taken (the edge proxy).</param>
/// <param name="CookiePrefix">The prefix of the session and antiforgery cookies.</param>
/// <param name="CookieSecurePolicy">Whether those cookies are Secure.</param>
/// <param name="CookieDomain">Their Domain attribute (the site's host), or null for none.</param>
public sealed record HostingProfile(HostingMode Mode, bool UsesHttps, bool TrustsForwardedScheme, string CookiePrefix, CookieSecurePolicy CookieSecurePolicy, string? CookieDomain)
{
    /// <summary>
    /// The profile of <c>ServerSetting:HostingMode</c> (any case) and <c>ServerSetting:DomainName</c>. Without a mode the site is the
    /// Tunnel deployment it always was; an unknown mode, or Tunnel / PortForwarding named without a domain, refuses to start.
    /// </summary>
    public static HostingProfile Create(string? mode, string? domainName)
    {
        string domain = domainName ?? "";
        if (string.IsNullOrWhiteSpace(mode))
            return Edge(HostingMode.Tunnel, domain);
        if (!Enum.TryParse(mode, ignoreCase: true, out HostingMode parsed) || !Enum.IsDefined(parsed))
            throw new InvalidOperationException($"ServerSetting:HostingMode '{mode}' is not one of {string.Join(", ", Enum.GetNames<HostingMode>())}.");
        if (parsed == HostingMode.NoDomain)
            return new HostingProfile(parsed, false, false, "", CookieSecurePolicy.SameAsRequest, null);
        if (string.IsNullOrWhiteSpace(domain))
            throw new InvalidOperationException($"ServerSetting:DomainName is required when ServerSetting:HostingMode is {parsed}.");
        return Edge(parsed, domain);
    }

    /// <summary>A profile with HTTPS at the edge; the cookie domain is the domain's host (as the site always set it).</summary>
    private static HostingProfile Edge(HostingMode mode, string domain) =>
        new(mode, true, true, "__Secure-", CookieSecurePolicy.Always,
            domain.Replace("https://", "", StringComparison.Ordinal).Replace("/", "", StringComparison.Ordinal));
}
