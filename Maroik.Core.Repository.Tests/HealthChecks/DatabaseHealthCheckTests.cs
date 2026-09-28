using Maroik.Core.PostgreSQL.Data;
using Maroik.Core.Repository.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Maroik.Core.Repository.Tests.HealthChecks;

/// <summary>
/// Unit tests for <see cref="DatabaseHealthCheck"/>. The healthy path runs against this class's real
/// throwaway PostgreSQL database (via <see cref="RepositoryTestBase"/>). For the unhealthy paths:
/// EF Core's <c>Database.CanConnectAsync</c> is itself defensive -- an unreachable host, and even a
/// malformed connection string, both come back as a plain <see langword="false"/> rather than
/// throwing, so those hit the check's "returned false" branch. To reach the check's own
/// <c>catch</c> block (and confirm it also reports Unhealthy, with the exception attached, instead
/// of propagating) a disposed context is used instead, which reliably throws
/// <see cref="ObjectDisposedException"/> before <c>CanConnectAsync</c> can even run.
/// </summary>
public class DatabaseHealthCheckTests(DatabaseFixture database) : RepositoryTestBase(database)
{
    /// <summary>A reachable database (the real seeded test database) is reported healthy.</summary>
    [Fact]
    public async Task CheckHealthAsync_DatabaseReachable_ReturnsHealthy()
    {
        var sut = new DatabaseHealthCheck(Context);

        HealthCheckResult result = await sut.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    /// <summary>
    /// An unreachable database (nothing listening on the given port): Npgsql's <c>CanConnectAsync</c>
    /// catches the connection failure and returns <see langword="false"/> rather than throwing, so
    /// this hits the check's "returned false" branch (no exception attached), not its catch block.
    /// </summary>
    [Fact]
    public async Task CheckHealthAsync_DatabaseUnreachable_ReturnsUnhealthy()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            // Port 1 is a well-known unassigned port -- the connection attempt fails fast
            // (connection refused) instead of hanging on Npgsql's default connect timeout.
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=nonexistent;Username=nonexistent;Password=nonexistent;Timeout=2")
            .Options;
        await using var unreachableContext = new ApplicationDbContext(options);
        var sut = new DatabaseHealthCheck(unreachableContext);

        HealthCheckResult result = await sut.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("Database.CanConnectAsync returned false.", result.Description);
        Assert.Null(result.Exception);
    }

    /// <summary>
    /// A context that throws instead of merely returning false (simulated here with an already-
    /// disposed context, which reliably throws <see cref="ObjectDisposedException"/>) is caught by
    /// the check's own try/catch and reported Unhealthy with the exception attached.
    /// </summary>
    [Fact]
    public async Task CheckHealthAsync_ThrowsBeforeCanConnectRuns_ReturnsUnhealthy_WithException()
    {
        var brokenContext = NewDbContext();
        await brokenContext.DisposeAsync();
        var sut = new DatabaseHealthCheck(brokenContext);

        HealthCheckResult result = await sut.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("Failed to connect to the database.", result.Description);
        Assert.IsType<ObjectDisposedException>(result.Exception);
    }
}
