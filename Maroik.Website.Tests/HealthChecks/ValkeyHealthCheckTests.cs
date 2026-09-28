using Maroik.Website.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using StackExchange.Redis;

namespace Maroik.Website.Tests.HealthChecks;

/// <summary>
/// Unit tests for <see cref="ValkeyHealthCheck"/>. <see cref="IConnectionMultiplexer"/> and
/// <see cref="IDatabase"/> are replaced with Moq mocks so both the reachable and unreachable
/// paths can be exercised without a real Valkey instance.
/// </summary>
public class ValkeyHealthCheckTests
{
    private readonly Mock<IConnectionMultiplexer> _connectionMultiplexer = new();
    private readonly Mock<IDatabase> _database = new();

    private ValkeyHealthCheck CreateSut() => new(_connectionMultiplexer.Object);

    /// <summary>Verifies a successful PING is reported as healthy.</summary>
    [Fact]
    public async Task CheckHealthAsync_PingSucceeds_ReturnsHealthy()
    {
        _connectionMultiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_database.Object);
        _database.Setup(d => d.PingAsync(It.IsAny<CommandFlags>())).ReturnsAsync(TimeSpan.FromMilliseconds(1));
        var sut = CreateSut();

        HealthCheckResult result = await sut.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    /// <summary>Verifies a connection failure is reported as unhealthy rather than throwing.</summary>
    [Fact]
    public async Task CheckHealthAsync_PingThrows_ReturnsUnhealthy()
    {
        _connectionMultiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_database.Object);
        _database.Setup(d => d.PingAsync(It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "unreachable"));
        var sut = CreateSut();

        HealthCheckResult result = await sut.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}
