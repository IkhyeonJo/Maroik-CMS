using System.Diagnostics;
using Docker.DotNet;
using Docker.DotNet.Models;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;

namespace Maroik.E2E.Tests.Infrastructure;

/// <summary>
/// Tests of <see cref="OtherTestcontainers"/> against the real Docker daemon. The "other session" is a container this
/// test starts by hand with the Testcontainers labels of a made-up session. It runs without a network
/// (<c>NetworkMode = none</c>), so starting and removing it never touches the bridge the browser uses. In the E2E
/// collection, so it never runs alongside a browser test.
/// </summary>
[Collection("E2E")]
public sealed class OtherTestcontainersTests : IAsyncDisposable
{
    /// <summary>A client of the same Docker daemon Testcontainers uses.</summary>
    private readonly IDockerClient _docker = TestcontainersSettings.OS.DockerEndpointAuthConfig
        .GetDockerClientBuilder(ResourceReaper.DefaultSessionId).Build();

    /// <summary>Id of the container started by <see cref="StartOtherSessionContainerAsync"/>, removed on dispose.</summary>
    private string? _containerId;

    /// <summary>
    /// Starts a long-sleeping container labelled as belonging to another Testcontainers session. Uses an image every
    /// Testcontainers run here has already pulled (the database image).
    /// </summary>
    private async Task StartOtherSessionContainerAsync()
    {
        var created = await _docker.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Image = "postgres:17",
            Entrypoint = ["sleep"],
            Cmd = ["300"],
            Labels = new Dictionary<string, string>
            {
                [OtherTestcontainers.Label] = "true",
                [OtherTestcontainers.SessionIdLabel] = $"other-{Guid.NewGuid():N}"
            },
            HostConfig = new HostConfig { NetworkMode = "none", AutoRemove = false }
        }, TestContext.Current.CancellationToken);
        _containerId = created.ID;
        await _docker.Containers.StartContainerAsync(created.ID, new ContainerStartParameters(), TestContext.Current.CancellationToken);
    }

    /// <summary>Force-removes <see cref="_containerId"/> (if still there).</summary>
    private async Task RemoveOtherSessionContainerAsync()
    {
        if (_containerId is null) return;
        try
        {
            await _docker.Containers.RemoveContainerAsync(_containerId, new ContainerRemoveParameters { Force = true });
        }
        catch (DockerContainerNotFoundException)
        {
            // Already removed by the test itself.
        }
        _containerId = null;
    }

    /// <summary>A running container of another session holds the wait until it is gone, and then the wait succeeds.</summary>
    [Fact]
    public async Task WaitUntilGone_WaitsForAnotherSessionsContainer_ThenSucceeds()
    {
        await StartOtherSessionContainerAsync();
        var removal = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            await RemoveOtherSessionContainerAsync();
        }, TestContext.Current.CancellationToken);
        var clock = Stopwatch.StartNew();

        bool gone = await OtherTestcontainers.WaitUntilGoneAsync(
            _docker, ResourceReaper.DefaultSessionId, TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
        TimeSpan waited = clock.Elapsed;

        await removal;
        Assert.True(gone);
        Assert.True(waited >= TimeSpan.FromSeconds(2), $"returned after {waited} while the container still ran");
    }

    /// <summary>A container of another session that outlives the timeout makes the wait give up and report it.</summary>
    [Fact]
    public async Task WaitUntilGone_GivesUpAfterTheTimeout_WhileAnotherSessionsContainerStillRuns()
    {
        await StartOtherSessionContainerAsync();
        var clock = Stopwatch.StartNew();

        bool gone = await OtherTestcontainers.WaitUntilGoneAsync(
            _docker, ResourceReaper.DefaultSessionId, TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        Assert.False(gone);
        Assert.True(clock.Elapsed >= TimeSpan.FromSeconds(1), $"gave up after {clock.Elapsed}, before the timeout");
    }

    /// <summary>The run's own containers (its database and resource reaper) never hold the wait.</summary>
    [Fact]
    public async Task WaitUntilGone_IgnoresTheRunsOwnContainers()
    {
        bool gone = await OtherTestcontainers.WaitUntilGoneAsync(
            _docker, ResourceReaper.DefaultSessionId, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        Assert.True(gone);
    }

    /// <summary>Removes any container a test left behind and releases the client.</summary>
    public async ValueTask DisposeAsync()
    {
        await RemoveOtherSessionContainerAsync();
        _docker.Dispose();
    }
}
