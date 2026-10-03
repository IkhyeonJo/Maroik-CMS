using System.Diagnostics;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace Maroik.E2E.Tests.Infrastructure;

/// <summary>
/// Waits out the containers another Testcontainers session (an earlier or concurrent test run) still has running.
/// </summary>
/// <remarks>
/// When a container leaves the Docker bridge network, Chrome treats it as a network change and aborts every request
/// in flight with <c>net::ERR_NETWORK_CHANGED</c>; the layout's global <c>ajaxError</c> handler then sends the page
/// to the public dashboard and the test fails. Run back-to-back, the previous run's resource reaper (Ryuk) is still
/// alive when this run starts and leaves the bridge a few seconds into it, in the middle of whichever test happens
/// to be waiting on a request.
/// </remarks>
public static class OtherTestcontainers
{
    /// <summary>Label Testcontainers puts on every container it starts, the resource reaper included.</summary>
    public const string Label = "org.testcontainers";

    /// <summary>Label holding the id of the Testcontainers session that started the container.</summary>
    public const string SessionIdLabel = "org.testcontainers.session-id";

    /// <summary>
    /// Returns <see langword="true"/> once no running container labelled <see cref="Label"/> belongs to a session
    /// other than <paramref name="ownSessionId"/>, or <see langword="false"/> when some are still running after
    /// <paramref name="timeout"/>.
    /// </summary>
    public static async Task<bool> WaitUntilGoneAsync(
        IDockerClient docker, Guid ownSessionId, TimeSpan timeout, TimeSpan pollInterval, CancellationToken ct)
    {
        var clock = Stopwatch.StartNew();
        while (true)
        {
            // Running containers only: a stopped one is no longer on the bridge, so it cannot cause a network change.
            IList<ContainerListResponse> running = await docker.Containers.ListContainersAsync(new ContainersListParameters
            {
                Filters = new Dictionary<string, IDictionary<string, bool>> { ["label"] = new Dictionary<string, bool> { [Label] = true } }
            }, ct);

            if (!running.Any(container => BelongsToAnotherSession(container, ownSessionId)))
                return true;
            if (clock.Elapsed >= timeout)
                return false;

            await Task.Delay(pollInterval, ct);
        }
    }

    /// <summary>True unless <paramref name="container"/>'s session label names <paramref name="ownSessionId"/>.</summary>
    private static bool BelongsToAnotherSession(ContainerListResponse container, Guid ownSessionId) =>
        !(container.Labels.TryGetValue(SessionIdLabel, out string? sessionId)
          && Guid.TryParse(sessionId, out Guid id)
          && id == ownSessionId);
}
