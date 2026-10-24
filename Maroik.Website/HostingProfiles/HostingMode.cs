namespace Maroik.Website.HostingProfiles;

/// <summary>How visitors reach the site (chosen by Deploy.sh, set as <c>ServerSetting:HostingMode</c>).</summary>
public enum HostingMode
{
    /// <summary>Through a Cloudflare Tunnel: HTTPS ends at Cloudflare, cloudflared forwards plain HTTP.</summary>
    Tunnel,

    /// <summary>Through port forwarding to this server: Caddy ends HTTPS (a Let's Encrypt certificate) and forwards plain HTTP.</summary>
    PortForwarding,

    /// <summary>Without a domain: plain HTTP straight to the website, for a home network.</summary>
    NoDomain,
}
