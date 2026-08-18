using Disney.Application;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Disney.Api;

internal sealed class DatabaseHealthCheck(
    IDatabaseHealthCheck database) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await database.CheckAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connectivity failed.", ex);
        }
    }
}
