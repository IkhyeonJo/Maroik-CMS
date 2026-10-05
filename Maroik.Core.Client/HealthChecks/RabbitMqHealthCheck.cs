using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace Maroik.Core.Client.HealthChecks;

/// <summary>
/// Reports healthy only if a new AMQP connection can actually be opened. Opens and immediately
/// closes a throwaway connection per check rather than sharing
/// <see cref="Maroik.Core.Client.Clients.RabbitMqEmailPublisher"/>'s long-lived connection, so a
/// stuck publisher connection doesn't mask an otherwise-healthy broker (or vice versa).
/// </summary>
public sealed class RabbitMqHealthCheck(string connectionString) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var factory = new ConnectionFactory { Uri = new Uri(connectionString) };
            await using IConnection connection = await factory.CreateConnectionAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            // Deliberately not logged: the failure is reported as this check's own result (with the
            // exception attached), which the health check middleware surfaces.
            return HealthCheckResult.Unhealthy("Failed to connect to RabbitMQ.", ex);
        }
    }
}
