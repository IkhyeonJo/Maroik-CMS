using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Reflection;
using Maroik.Core.Domain.Localization;
using Maroik.Website.Contracts;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// The composition root (<c>Program.cs</c>) fails fast on unusable server settings and wires the hot-swappable TLS
/// certificate manager only when both certificate paths are configured. These start derived hosts with host-level
/// settings (<c>UseSetting</c> — the only kind <c>Program.cs</c> sees while it is still building the host).
/// </summary>
[Collection("Website Integration")]
public class ProgramStartupTests(MaroikWebApplicationFactory factory)
{
    private static Exception? StartupFailure(Func<HttpClient> start)
    {
        try { start().Dispose(); return null; }
        catch (Exception e) { return e is TargetInvocationException { InnerException: { } inner } ? inner : e; }
    }

    /// <summary>A non-positive session lifetime would issue instantly-expired cookies, so start-up refuses it.</summary>
    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    public void Startup_Throws_WhenSessionExpireMinutesIsNotPositive(string value)
    {
        using var factory1 = factory.WithWebHostBuilder(b => b.UseSetting("ServerSetting:SessionExpireMinutes", value));

        Exception? failure = StartupFailure(factory1.CreateClient);

        Assert.IsType<InvalidOperationException>(failure);
        Assert.Contains("SessionExpireMinutes", failure.Message);
    }

    /// <summary>Zero permitted login attempts would lock every account on its first try, so start-up refuses it.</summary>
    [Fact]
    public void Startup_Throws_WhenMaxLoginAttemptIsZero()
    {
        using var factory1 = factory.WithWebHostBuilder(b => b.UseSetting("ServerSetting:MaxLoginAttempt", "0"));

        Exception? failure = StartupFailure(factory1.CreateClient);

        Assert.IsType<InvalidOperationException>(failure);
        Assert.Contains("MaxLoginAttempt", failure.Message);
    }

    /// <summary>With both certificate paths configured the manager is registered and serves the certificate from disk.</summary>
    [Fact]
    public void Startup_RegistersTheCertificateManager_WhenBothCertificatePathsAreConfigured()
    {
        string dir = Directory.CreateTempSubdirectory("maroik-cert-").FullName;
        try
        {
            using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var request = new CertificateRequest("CN=www.localhost", key, HashAlgorithmName.SHA256);
            using X509Certificate2 cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            string certPath = Path.Combine(dir, "cert.pem");
            string keyPath = Path.Combine(dir, "privkey.pem");
            File.WriteAllText(certPath, cert.ExportCertificatePem());
            File.WriteAllText(keyPath, key.ExportECPrivateKeyPem());

            using var factory1 = factory.WithWebHostBuilder(b => b
                .UseSetting("ServerSetting:DockerCertPath", certPath)
                .UseSetting("ServerSetting:DockerKeyPath", keyPath));
            using HttpClient client = factory1.CreateClient();

            var manager = factory1.Services.GetRequiredService<ICertificateManager>();
            Assert.Equal(cert.Thumbprint, manager.SelectCertificate().Thumbprint);

            // Kestrel's HTTPS defaults pick the certificate from that manager on every handshake (so a renewed one is served
            // without a restart). ApplyHttpsDefaults is Kestrel-internal, so it is invoked by reflection.
            var kestrel = factory1.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value;
            var https = new HttpsConnectionAdapterOptions();
            typeof(KestrelServerOptions).GetMethod("ApplyHttpsDefaults", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(kestrel, [https]);
            Assert.NotNull(https.ServerCertificateSelector);
            Assert.Equal(cert.Thumbprint, https.ServerCertificateSelector!(null, null)!.Thumbprint);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>Without certificate paths (the test default) no manager is registered and no certificate polling starts.</summary>
    [Fact]
    public void Startup_DoesNotRegisterTheCertificateManager_WithoutCertificatePaths()
    {
        using HttpClient client = factory.CreateClient();

        Assert.Null(factory.Services.GetService<ICertificateManager>());
    }

    /// <summary>Without a Valkey connection string the Data Protection keyring is persisted to a folder under the content root.</summary>
    [Fact]
    public void Startup_PersistsTheDataProtectionKeyRingToDisk_WithoutValkey()
    {
        using var factory1 = factory.WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:Valkey", ""));
        using HttpClient client = factory1.CreateClient();

        string contentRoot = factory1.Services.GetRequiredService<IHostEnvironment>().ContentRootPath;
        Assert.True(Directory.Exists(Path.Combine(contentRoot, "dataprotection-keys")));
    }

    /// <summary>The site's request cultures come from <c>CulturePolicy</c> (the Domain's single list of supported cultures).</summary>
    [Fact]
    public void Startup_ConfiguresRequestLocalization_FromCulturePolicy()
    {
        using HttpClient client = factory.CreateClient();

        RequestLocalizationOptions options = factory.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;

        Assert.Equal(CulturePolicy.DefaultCulture, options.DefaultRequestCulture.Culture.Name);
        Assert.Equal(CulturePolicy.SupportedCultures.OrderBy(c => c), options.SupportedCultures!.Select(c => c.Name).OrderBy(c => c));
        Assert.Equal(CulturePolicy.SupportedCultures.OrderBy(c => c), options.SupportedUICultures!.Select(c => c.Name).OrderBy(c => c));
    }

    /// <summary>With a Valkey connection string the shared multiplexer backs the distributed cache, namespaced under "MaroikWebsite:".</summary>
    [Fact]
    public async Task Startup_BacksTheDistributedCacheWithTheSharedValkeyMultiplexer()
    {
        using HttpClient client = factory.CreateClient();

        RedisCacheOptions options = factory.Services.GetRequiredService<IOptions<RedisCacheOptions>>().Value;

        Assert.Equal("MaroikWebsite:", options.InstanceName);
        Assert.NotNull(options.ConnectionMultiplexerFactory);
        Assert.Same(factory.Services.GetRequiredService<IConnectionMultiplexer>(), await options.ConnectionMultiplexerFactory!());
    }
}
