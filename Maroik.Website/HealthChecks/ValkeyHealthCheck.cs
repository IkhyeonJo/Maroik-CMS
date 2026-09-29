using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Maroik.Website.HealthChecks;

/// <summary>
/// Reports healthy only if Valkey actually responds to a PING. Only registered when a Valkey
/// connection string is configured (see Program.cs) - otherwise the app falls back to an in-memory
/// cache and there is nothing to check.
/// </summary>
public sealed class ValkeyHealthCheck(IConnectionMultiplexer connectionMultiplexer) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await connectionMultiplexer.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            // Deliberately not logged: the failure is reported as this check's own result (with the
            // exception attached), which the health-check middleware surfaces.
            return HealthCheckResult.Unhealthy("Failed to PING Valkey.", ex);
        }
    }
}
