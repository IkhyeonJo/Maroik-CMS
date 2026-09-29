using Maroik.Core.PostgreSQL.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Maroik.Core.Repository.HealthChecks;

/// <summary>
/// Reports healthy only if the PostgreSQL database is actually reachable, so a deployment that starts
/// fine but can't talk to the database is caught by a health probe on <c>/health</c> instead of
/// being reported as a successful deployment.
/// </summary>
public sealed class DatabaseHealthCheck(ApplicationDbContext dbContext) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Database.CanConnectAsync returned false.");
        }
        catch (Exception ex)
        {
            // Deliberately not logged: the failure is reported as this check's own result (with the
            // exception attached), which the health-check middleware surfaces.
            return HealthCheckResult.Unhealthy("Failed to connect to the database.", ex);
        }
    }
}
