using Docker.DotNet;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Microsoft.Playwright;
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MethodHasAsyncOverload

namespace Maroik.E2E.Tests.Infrastructure;

/// <summary>
/// Shared fixture for all E2E test classes in the "E2E" collection.
/// Creates ONE seeded PostgreSQL container (<see cref="E2EPostgresContainer"/>), ONE
/// <see cref="PlaywrightWebApplicationFactory"/> (so Program.cs runs exactly once per test
/// session) and ONE headless browser instance (Chrome by default; see <see cref="BrowserName"/>).
/// Each test creates its own <see cref="IBrowserContext"/> via
/// <see cref="PlaywrightWebApplicationFactory.ServerBaseUrl"/>, ensuring page isolation.
/// </summary>
public sealed class E2ESharedFixture : IAsyncLifetime
{
    /// <summary>The database the website under test uses.</summary>
    private readonly E2EPostgresContainer _database = new();
    /// <summary>The website under test; set in <c>InitializeAsync</c>.</summary>
    private PlaywrightWebApplicationFactory? _factory;
    /// <summary>The Playwright driver; set in <c>InitializeAsync</c>.</summary>
    private IPlaywright? _playwright;
    /// <summary>The shared headless browser; set in <c>InitializeAsync</c>.</summary>
    private IBrowser? _browser;

    /// <summary>The shared headless browser instance used by every test.</summary>
    public IBrowser Browser => _browser
        ?? throw new InvalidOperationException("The shared browser has not been initialized.");

    /// <summary>Direct access to the seeded database, for seeding fixtures and asserting persisted rows.</summary>
    public E2EDatabase Database => new(_database.ConnectionString);

    /// <summary>Base URL of the in-process test server hosting the application.</summary>
    public string ServerBaseUrl => _factory!.ServerBaseUrl;

    /// <summary>The in-memory file storage the website under test reads and writes.</summary>
    public E2EFileStorage Files => _factory!.Files;

    /// <summary>Starts the database container and in-process server, then launches the shared browser instance.</summary>
    public async ValueTask InitializeAsync()
    {
        await WaitForOtherTestRunsContainersAsync();
        await _database.StartAsync();

        _factory = new PlaywrightWebApplicationFactory(_database.ConnectionString);
        _factory.CreateClient();

        _playwright = await Playwright.CreateAsync();
        _browser = await LaunchBrowserAsync(_playwright, BrowserName);
    }

    /// <summary>
    /// Before this run starts its own containers, waits (up to a minute) until no other Testcontainers session still has
    /// one running — typically the previous run's resource reaper, which otherwise leaves the Docker bridge a few
    /// seconds into this run and makes the browser abort a request in flight (see <see cref="OtherTestcontainers"/>).
    /// A run of another suite that is still going is not waited out to its end: after the minute the run proceeds
    /// and says so on stderr.
    /// </summary>
    private static async Task WaitForOtherTestRunsContainersAsync()
    {
        using IDockerClient docker = TestcontainersSettings.OS.DockerEndpointAuthConfig
            .GetDockerClientBuilder(ResourceReaper.DefaultSessionId).Build();
        bool quiet = await OtherTestcontainers.WaitUntilGoneAsync(
            docker, ResourceReaper.DefaultSessionId, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1), CancellationToken.None);
        if (!quiet)
            await Console.Error.WriteLineAsync(
                "E2E: another Testcontainers session still has containers running; starting anyway. " +
                "A browser request may fail with net::ERR_NETWORK_CHANGED when they stop.");
    }

    /// <summary>
    /// Environment variable that picks the browser the whole run uses: <c>chrome</c> (default, the
    /// installed Google Chrome), <c>chromium</c> (Playwright's bundled build) or <c>firefox</c>.
    /// CI runs the suite once per browser.
    /// </summary>
    private const string BrowserEnvironmentVariable = "E2E_BROWSER";

    /// <summary>The browser this run drives, from <see cref="BrowserEnvironmentVariable"/>.</summary>
    private static string BrowserName =>
        Environment.GetEnvironmentVariable(BrowserEnvironmentVariable) is { Length: > 0 } name
            ? name.Trim().ToLowerInvariant()
            : "chrome";

    /// <summary>Launches a headless browser for <paramref name="browserName"/> (chrome, chromium or firefox); any other name throws.</summary>
    private static Task<IBrowser> LaunchBrowserAsync(IPlaywright playwright, string browserName) => browserName switch
    {
        "chrome" => playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Channel = "chrome",
            Args = ["--no-sandbox"]
        }),
        "chromium" => playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = ["--no-sandbox"]
        }),
        // Chromium's --no-sandbox is not a Firefox flag; Playwright's bundled Firefox needs none.
        "firefox" => playwright.Firefox.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true }),
        _ => throw new InvalidOperationException(
            $"Unknown {BrowserEnvironmentVariable} '{browserName}'. Use chrome, chromium or firefox.")
    };

    /// <summary>Tears down the browser, Playwright driver, in-process server, and database container.</summary>
    /// <remarks>
    /// Every step is null-guarded: if <see cref="InitializeAsync"/> throws part-way (e.g. the host
    /// fails to start), xUnit still calls this, and it must surface that original error rather than
    /// mask it with a <see cref="NullReferenceException"/> on a member that was never assigned.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (_browser is not null) await _browser.DisposeAsync();
        _playwright?.Dispose();
        _factory?.Dispose();
        await _database.DisposeAsync();
    }
}
