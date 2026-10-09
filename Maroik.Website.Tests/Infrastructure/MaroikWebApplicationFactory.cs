using System.Security.Cryptography;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Account;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Core.PostgreSQL.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
// ReSharper disable ClassNeverInstantiated.Global

namespace Maroik.Website.Tests.Infrastructure;

/// <summary>
/// Custom <see cref="WebApplicationFactory{TEntryPoint}"/> for <c>Maroik.Website</c>.
///
/// Overrides applied for testing:
/// <list type="bullet">
///   <item>Configuration: empty cert paths (skips TLS hot-swap logic), test domain, short session timeout.</item>
///   <item>Database: a real PostgreSQL instance spun up via Testcontainers, schema loaded from
///       the actual init script (<c>Maroik.DB/PostgreSQL/SQL_Init_Script/Debugging/Init.sql</c>)
///       rather than EF Core's InMemory provider or <c>EnsureCreated()</c> — both of the latter
///       build schema from the EF model's Fluent API config, which does not necessarily match
///       the hand-authored SQL (it omits the <c>ON UPDATE CASCADE</c> clauses the real script
///       has on the Asset foreign keys, for one). Running the real script means transaction-
///       wrapped service methods, raw-SQL repository queries (<c>FOR UPDATE</c> row locks,
///       composite-key updates), and constraint behavior all work exactly as in production.</item>
///   <item>Base address: <c>https://www.localhost/</c> so the <c>UseHttpsRedirection</c> and
///       <c>AddRedirectToWww</c> middleware do not trigger additional redirects.</item>
/// </list>
/// One container is started per test run (shared via <see cref="WebsiteIntegrationCollection"/>'s
/// <c>ICollectionFixture</c>) and is disposed when the collection finishes.
/// </summary>
public class MaroikWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>
    /// A real, freshly-generated RSA keypair (not the empty strings production leaves out of
    /// source control) so <c>RsaService</c>'s <c>Encrypt</c>/<c>Decrypt</c> — used for
    /// registration/password-reset tokens and, separately, file-storage paths (see project
    /// memory: paths are RSA-encrypted with a server-only key) — work for real. Without this,
    /// every flow that round-trips a token (Register's confirmation email, ConfirmEmail,
    /// ForgotPassword, ResetPassword) would throw "RSA private/public key is not configured"
    /// the moment it tried to encrypt or decrypt a token.
    /// </summary>
    private static readonly RSA _rsaKeyPair = RSA.Create(2048);

    /// <summary>Throwaway PostgreSQL 17 container initialized from the production <c>Init.sql</c>.</summary>
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17")
        // Schema comes from the real init script (not EF's EnsureCreated, which
        // builds from the EF model's Fluent API config — a config that turned out to omit the
        // ON UPDATE CASCADE clauses the real script defines on Income_fk_0/Expenditure_fk_0/
        // FixedIncome_fk_0/FixedExpenditure_fk_0). Files under docker-entrypoint-initdb.d/ are
        // run automatically via psql on first container startup, which correctly understands
        // pg_dump's \restrict/\unrestrict meta-commands (a raw ADO.NET batch would not).
        .WithResourceMapping(new FileInfo(FindInitSqlPath()), new FileInfo("/docker-entrypoint-initdb.d/init.sql"))
        .Build();
    /// <summary>Connection string of <see cref="_postgres"/>; set once it has started.</summary>
    private string _connectionString = "";

    /// <summary>Absolute path of the production <c>Init.sql</c>, found by walking up to the directory holding <c>Maroik.sln</c>.</summary>
    private static string FindInitSqlPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Maroik.sln")))
            dir = dir.Parent;
        return dir == null ? throw new InvalidOperationException("Could not locate the repository root (Maroik.sln) from " + AppContext.BaseDirectory) : Path.Combine(dir.FullName, "Maroik.DB", "PostgreSQL", "SQL_Init_Script", "Debugging", "Init.sql");
    }

    /// <summary>Starts the Testcontainers PostgreSQL instance before any test in the collection runs.</summary>
    public async ValueTask InitializeAsync()
    {
        // Npgsql 9+ requires DateTimeKind.Unspecified for timestamp-without-timezone columns;
        // this restores the lenient behavior the seed helpers across these tests rely on
        // (they all pass DateTimeKind.Utc via DateTime.UtcNow).
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        await _postgres.StartAsync();
        _connectionString = _postgres.GetConnectionString();
    }

    /// <summary>Stops the Testcontainers PostgreSQL instance after the collection finishes.</summary>
    public new async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    /// <summary>Points the application at the Testcontainers PostgreSQL instance and applies test-only configuration overrides.</summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // -- Do not watch the configuration files -----------------------------
        // Each watched file holds an inotify instance for the host's lifetime. This factory and every
        // WithWebHostBuilder host derived from it stay alive for the whole run, so watching would exhaust
        // the per-user inotify limit (128 by default) and every later host would fail to start. Nothing
        // here edits appsettings*.json during a run, so nothing is lost.
        builder.UseSetting("hostBuilder:reloadConfigOnChange", "false");

        // -- Override configuration -------------------------------------------
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string>
            {
                // Use a test domain; www prefix avoids AddRedirectToWww triggering
                ["ServerSetting:DomainName"] = "https://www.localhost",
                ["ServerSetting:SessionExpireMinutes"] = "60",
                ["ServerSetting:MaxLoginAttempt"] = "5",
                ["ServerSetting:NoticeMaturityDateDay"] = "7",
                ["ServerSetting:RsaPrivateKey"] = Convert.ToBase64String(_rsaKeyPair.ExportRSAPrivateKey()),
                ["ServerSetting:RsaPublicKey"] = Convert.ToBase64String(_rsaKeyPair.ExportSubjectPublicKeyInfo()),
                ["ServerSetting:RsaAlgorithm"] = "Rsa2",
                ["ServerSetting:FileStorageBaseUrl"] = "http://localhost:5001",

                // SMTP — not used in route tests
                ["ServerSetting:SmtpOptions:smtpUserName"] = "test",
                ["ServerSetting:SmtpOptions:smtpPassword"] = "test",
                ["ServerSetting:SmtpOptions:smtpHost"] = "localhost",
                ["ServerSetting:SmtpOptions:smtpPort"] = "25",
                ["ServerSetting:SmtpOptions:smtpSsl"] = "false",
                ["ServerSetting:SmtpOptions:fromEmail"] = "test@localhost",
                ["ServerSetting:SmtpOptions:fromFullName"] = "Test",
                ["ServerSetting:SmtpOptions:IsDefault"] = "true",

                // ClamAV — not used in route tests
                ["Clamav:Host"] = "localhost",
                ["Clamav:Port"] = "3310",

                // FileStorage
                ["FileStorage:BaseUrl"] = "http://localhost:5001",

                // DB — replaced below by the Testcontainers PostgreSQL connection, but a value is required to avoid null errors
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test",
                ["ConnectionStrings:Valkey"] = ""
            }!);
        });

        // -- Point PostgreSQL at the Testcontainers instance ------------------
        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext registrations registered by AddRepositoryContext()
            var dbOptionsDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (dbOptionsDescriptor != null) services.Remove(dbOptionsDescriptor);

            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(ApplicationDbContext));
            if (dbContextDescriptor != null) services.Remove(dbContextDescriptor);

            services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(_connectionString));

            // Replace the RabbitMQ-backed email publisher with an in-memory fake so Register/
            // ForgotPassword flows can be exercised without a live broker (RabbitMqEmailPublisher
            // itself is already unit-tested for real in Maroik.Core.Client.Tests).
            var emailPublisherDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IEmailPublisher));
            if (emailPublisherDescriptor != null) services.Remove(emailPublisherDescriptor);
            services.AddSingleton<FakeEmailPublisher>();
            services.AddSingleton<IEmailPublisher>(sp => sp.GetRequiredService<FakeEmailPublisher>());

            // Replace Valkey distributed cache with in-memory so tests run without a Valkey instance.
            var distributedCacheDescriptors = services
                .Where(d => d.ServiceType == typeof(IDistributedCache))
                .ToList();
            foreach (var d in distributedCacheDescriptors) services.Remove(d);
            services.AddDistributedMemoryCache();

            // Replace Valkey-backed Data Protection with ephemeral in-memory keys.
            services.AddDataProtection().UseEphemeralDataProtectionProvider();

            // Program.cs reads DomainName before ConfigureAppConfiguration overrides take effect,
            // so Session/Antiforgery cookies end up with Domain=localhost (from appsettings.Development.json).
            // PostConfigure clears the domain so the CookieContainerHandler accepts cookies for www.localhost.
            services.PostConfigure<SessionOptions>(options => options.Cookie.Domain = null);
            services.PostConfigure<AntiforgeryOptions>(options => options.Cookie.Domain = null);
        });
    }

    /// <summary>
    /// Extra Category rows for menu areas the real production dump doesn't seed at all
    /// (Drive is unlaunched — see project memory) but that a handful of tests still need a
    /// Controller-name menu match for. IDs start well above the dump's real range (currently
    /// tops out at 21) to avoid colliding with its actual Category_pk values.
    /// </summary>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Idempotent: a derived factory (WithWebHostBuilder) runs this CreateHost again against
        // the same, already-seeded database.
        if (db.Categories.Any(c => c.Id == 9001))
            return host;

        db.Categories.AddRange(
            new Category { Id = 9001, Name = "DriveAnon", DisplayName = "Drive", IconPath = "", Controller = "Drive", Action = "AnonymousIndex", Role = Role.Anonymous, Order = 100 },
            new Category { Id = 9002, Name = "DriveAdmin", DisplayName = "Drive (Admin)", IconPath = "", Controller = "Drive", Action = "AdminIndex", Role = Role.Admin, Order = 101 },
            new Category { Id = 9003, Name = "DriveUser", DisplayName = "Drive (User)", IconPath = "", Controller = "Drive", Action = "UserIndex", Role = Role.User, Order = 102 }
        );
        db.SaveChanges();

        return host;
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> pre-configured to use <c>https://www.localhost/</c>
    /// as the base address. This prevents <c>UseHttpsRedirection</c> and <c>AddRedirectToWww</c>
    /// from issuing extra redirects during tests.
    /// </summary>
    public HttpClient CreateTestClient(bool followRedirects = false) =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://www.localhost/"),
            AllowAutoRedirect = followRedirects,
            // Every test manages cookies explicitly (session + antiforgery), so the client's own
            // auto-tracking CookieContainer must stay off — otherwise it re-attaches cookies from
            // earlier responses alongside the ones a test sets manually, sending a duplicate
            // Cookie header that can confuse request parsing/routing on some requests.
            HandleCookies = false
        });
}
