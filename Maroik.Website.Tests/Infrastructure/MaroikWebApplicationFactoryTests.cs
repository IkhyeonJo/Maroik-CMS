using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Maroik.Website.Tests.Infrastructure;

/// <summary>Tests of the test host itself (<see cref="MaroikWebApplicationFactory"/>).</summary>
[Collection("Website Integration")]
public class MaroikWebApplicationFactoryTests(MaroikWebApplicationFactory factory)
{
    /// <summary>
    /// The test host does not watch its configuration files. A watched file holds an inotify instance for the
    /// host's lifetime and test hosts are never stopped mid-run, so every derived host (<c>WithWebHostBuilder</c>)
    /// would take another one until the per-user limit (128 by default) is reached — after which every later host
    /// in the run fails to start ("The configured user limit (128) on the number of inotify instances has been
    /// reached").
    /// </summary>
    [Fact]
    public void TheTestHost_DoesNotWatchItsConfigurationFiles()
    {
        var configuration = (IConfigurationRoot)factory.Services.GetRequiredService<IConfiguration>();

        List<FileConfigurationProvider> fileProviders = [.. configuration.Providers.OfType<FileConfigurationProvider>()];

        Assert.NotEmpty(fileProviders);
        Assert.All(fileProviders, provider => Assert.False(provider.Source.ReloadOnChange, provider.Source.Path));
    }
}
