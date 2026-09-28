using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Maroik.E2E.Tests.Infrastructure;

/// <summary>
/// Extends <see cref="WebApplicationFactory{TEntryPoint}"/> to start a real Kestrel
/// server on a random available port so Playwright (which runs in a separate browser
/// process) can reach it via a real TCP connection.
///
/// Design note: the standard <c>TestServer</c> used by <see cref="WebApplicationFactory{T}"/>
/// is an in-process, socket-less transport that Playwright cannot access.  By overriding
/// <see cref="CreateHost"/> we spin up a <em>second</em> host that binds a real port while
/// still returning the original test-host so the base-class HttpClient helpers work normally.
///
/// The application talks to a real PostgreSQL 17 database (<see cref="E2EPostgresContainer"/>,
/// loaded from the <c>Init.sql</c>) — not the EF Core InMemory provider — so the
/// browser tests exercise the real schema, constraints and seeded navigation menu.
/// </summary>
public sealed class PlaywrightWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> _configOverrides;
    private IHost? _realHost;

    /// <summary>Base URL of the real Kestrel server (e.g. <c>http://localhost:57432</c>).</summary>
    public string ServerBaseUrl { get; private set; } = "";

    /// <param name="connectionString">
    /// Npgsql connection string to the seeded E2E database (from <see cref="E2EPostgresContainer"/>).
    /// </param>
    public PlaywrightWebApplicationFactory(string connectionString)
    {
        var s = connectionString;

        // The schema uses `timestamp without time zone`; Npgsql needs the legacy switch to accept
        // Utc/Local DateTime values. Program.cs sets it too, but the container may be queried
        // before the host is built.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        _configOverrides = new Dictionary<string, string?>
        {
            ["ServerSetting:DockerCertPath"] = "",
            ["ServerSetting:DockerKeyPath"] = "",
            ["ServerSetting:DomainName"] = "https://www.localhost",
            ["ServerSetting:SessionExpireMinutes"] = "60",
            ["ServerSetting:MaxLoginAttempt"] = "5",
            ["ServerSetting:NoticeMaturityDateDay"] = "7",
            ["ServerSetting:RsaPrivateKey"] = "",
            ["ServerSetting:RsaPublicKey"] = "",
            ["ServerSetting:RsaAlgorithm"] = "Rsa2",
            ["ServerSetting:FileStorageBaseUrl"] = "http://localhost:5001",
            ["ServerSetting:SmtpOptions:smtpUserName"] = "test",
            ["ServerSetting:SmtpOptions:smtpPassword"] = "test",
            ["ServerSetting:SmtpOptions:smtpHost"] = "localhost",
            ["ServerSetting:SmtpOptions:smtpPort"] = "25",
            ["ServerSetting:SmtpOptions:smtpSsl"] = "false",
            ["ServerSetting:SmtpOptions:fromEmail"] = "test@localhost",
            ["ServerSetting:SmtpOptions:fromFullName"] = "Test",
            ["ServerSetting:SmtpOptions:IsDefault"] = "true",
            ["Clamav:Host"] = "localhost",
            ["Clamav:Port"] = "3310",
            ["FileStorage:BaseUrl"] = "http://localhost:5001",
            // Real seeded PostgreSQL container — AddRepositoryContext() in Program.cs picks
            // this up and wires the DbContext to Npgsql, so no service-level DB swap is needed.
            ["ConnectionStrings:DefaultConnection"] = s,
            ["ConnectionStrings:Valkey"] = ""
        };

        // Program.cs (top-level statements) reads and *validates* several ServerSetting values
        // eagerly — before `builder.Build()` — e.g. `SessionExpireMinutes <= 0` now throws. A
        // WebApplicationFactory only merges its `ConfigureAppConfiguration` overrides at Build
        // time, so those eager reads would still see nothing and the host would never start.
        // Publish the overrides as process environment variables too: `AddEnvironmentVariables()`
        // in `WebApplication.CreateBuilder` picks them up immediately, so the pre-Build reads
        // resolve. Cleared again in Dispose so nothing leaks past this factory's lifetime.
        foreach (var (key, value) in _configOverrides)
            Environment.SetEnvironmentVariable(key.Replace(":", "__"), value);
    }

    // -- WebApplicationFactory overrides --------------------------------------

    /// <summary>Configures the "Testing" environment: in-memory settings overrides and the real Postgres connection string.</summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // -- Override configuration -------------------------------------------
        // Also injected via environment variables in the constructor (see there) so Program.cs's
        // pre-Build reads resolve; this in-memory layer keeps them authoritative post-Build.
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(_configOverrides));

        // -- Replace only the external infra that has no container here (Valkey) -------
        builder.ConfigureServices(services =>
        {
            // Replace Valkey distributed cache with in-memory so tests run without a Valkey instance.
            var distributedCacheDescriptors = services
                .Where(d => d.ServiceType == typeof(IDistributedCache))
                .ToList();
            foreach (var d in distributedCacheDescriptors) services.Remove(d);
            services.AddDistributedMemoryCache();

            // Replace Valkey-backed Data Protection with ephemeral in-memory keys.
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
        });
    }

    /// <summary>
    /// Builds the standard in-process test host, then additionally starts a real Kestrel
    /// HTTPS server on <see cref="ServerBaseUrl"/> so Playwright's browser (a separate OS
    /// process) can actually connect over the network, which the in-process test server alone
    /// does not allow.
    /// </summary>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Build the standard in-process test host (used by base-class HttpClient helpers)
        var testHost = builder.Build();

        // Build a second host with a real Kestrel HTTPS listener on any available port.
        // UseHttps() uses the ASP.NET Core dev certificate so UseHttpsRedirection()
        // in Program.cs does not issue a redirect (request is already HTTPS).
        builder.ConfigureWebHost(webHostBuilder =>
            webHostBuilder.UseKestrel(o => o.ListenAnyIP(0, l => l.UseHttps())));

        _realHost = builder.Build();
        _realHost.Start();

        // The anonymous Category / SubCategory rows AuthorizationFilter resolves against are
        // already present from Init.sql — no manual seeding needed.

        // Discover the actual port that Kestrel chose.
        // Use www.localhost directly so AddRedirectToWww() in Program.cs never fires
        // (the host already has the www. prefix and no redirect is issued).
        var server = _realHost.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>();
        var port = new Uri(addresses!.Addresses.Last()).Port;
        ServerBaseUrl = $"https://www.localhost:{port}";

        return testHost;
    }

    /// <summary>Disposes the real Kestrel host created in <see cref="CreateHost"/> alongside the base host.</summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var key in _configOverrides.Keys)
                Environment.SetEnvironmentVariable(key.Replace(":", "__"), null);
        }

        _realHost?.Dispose();
        base.Dispose(disposing);
    }
}
