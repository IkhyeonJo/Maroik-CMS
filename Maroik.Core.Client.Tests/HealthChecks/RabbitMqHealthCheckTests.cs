using Maroik.Core.Client.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Testcontainers.RabbitMq;

namespace Maroik.Core.Client.Tests.HealthChecks;

/// <summary>
/// Unit tests for <see cref="RabbitMqHealthCheck"/>. It opens a real AMQP connection per check
/// (there is no injectable connection abstraction to mock), so only the failure path is exercised
/// here without a real broker: an unreachable/malformed connection string must be reported as
/// <see cref="HealthStatus.Unhealthy"/>, with the underlying exception attached, rather than
/// propagating and taking the health check middleware down with it. The healthy path and a real
/// authentication refusal run against a throwaway RabbitMQ container (Testcontainers; Docker required,
/// like the Repository and Website tests).
/// </summary>
public class RabbitMqHealthCheckTests : IAsyncLifetime
{
    private readonly RabbitMqContainer _broker = new RabbitMqBuilder("rabbitmq:4-alpine").Build();

    /// <summary>Starts the broker container.</summary>
    public async ValueTask InitializeAsync() => await _broker.StartAsync();

    /// <summary>Stops and removes the broker container.</summary>
    public async ValueTask DisposeAsync()
    {
        await _broker.DisposeAsync();
        GC.SuppressFinalize(this);
    }
    
    /// <summary>A reachable broker with valid credentials is healthy.</summary>
    [Fact]
    public async Task CheckHealthAsync_ReachableBroker_ReturnsHealthy()
    {
        var sut = new RabbitMqHealthCheck(_broker.GetConnectionString());

        HealthCheckResult result = await sut.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Null(result.Exception);
    }

    /// <summary>A broker that is up but refuses the credentials is unhealthy, with the refusal attached.</summary>
    [Fact]
    public async Task CheckHealthAsync_ReachableBroker_WrongCredentials_ReturnsUnhealthy()
    {
        var sut = new RabbitMqHealthCheck($"amqp://nobody:wrong@{_broker.Hostname}:{_broker.GetMappedPublicPort(5672)}/");

        HealthCheckResult result = await sut.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("Failed to connect to RabbitMQ.", result.Description);
        Assert.NotNull(result.Exception);
    }

    /// <summary>A connection attempt to a port nothing is listening on is reported unhealthy, not thrown.</summary>
    [Fact]
    public async Task CheckHealthAsync_UnreachableBroker_ReturnsUnhealthy()
    {
        // Port 1 is a well-known unassigned port; nothing should ever be listening there, so the
        // connection attempt fails fast (connection refused) instead of hanging on a real timeout.
        var sut = new RabbitMqHealthCheck("amqp://guest:guest@127.0.0.1:1/");

        HealthCheckResult result = await sut.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("Failed to connect to RabbitMQ.", result.Description);
        Assert.NotNull(result.Exception);
    }

    /// <summary>A malformed connection string (not even a valid AMQP URI) is reported unhealthy rather than throwing out of the health check middleware.</summary>
    [Fact]
    public async Task CheckHealthAsync_MalformedConnectionString_ReturnsUnhealthy()
    {
        var sut = new RabbitMqHealthCheck("not a uri at all");

        HealthCheckResult result = await sut.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }
}
