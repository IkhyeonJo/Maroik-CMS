using System.Reflection;
using Maroik.Website.Constants;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// The composition root (<c>Program.cs</c>) fails fast on unusable server settings and wires the shared services. These
/// start derived hosts with host-level settings (<c>UseSetting</c> — the only kind <c>Program.cs</c> sees while it is
/// still building the host).
/// </summary>
[Collection("Website Integration")]
public class ProgramStartupTests(MaroikWebApplicationFactory factory)
{
    /// <summary>Runs <paramref name="start"/> and returns the exception start-up failed with (unwrapped from reflection), or <see langword="null"/>.</summary>
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

    /// <summary>Without a Valkey connection string the Data Protection keyring is persisted to a folder under the content root.</summary>
    [Fact]
    public void Startup_PersistsTheDataProtectionKeyRingToDisk_WithoutValkey()
    {
        using var factory1 = factory.WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:Valkey", ""));
        using HttpClient client = factory1.CreateClient();

        string contentRoot = factory1.Services.GetRequiredService<IHostEnvironment>().ContentRootPath;
        Assert.True(Directory.Exists(Path.Combine(contentRoot, "dataprotection-keys")));
    }

    /// <summary>The site's request cultures come from <c>CulturePolicy</c> (the Website's single list of supported cultures).</summary>
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
