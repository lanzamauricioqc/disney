using Disney.Application;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Disney.Api;

internal sealed class QueueTimesHealthCheck(
    IQueueTimesProvider queueTimesProvider,
    IOptions<QueueTimesHealthCheckOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await queueTimesProvider.GetQueueTimesForParkAsync(
                options.Value.SourceParkId,
                cancellationToken);
            return HealthCheckResult.Healthy(
                "Queue Times API returned valid queue data.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "Queue Times API connectivity or response validation failed.",
                exception);
        }
    }
}

internal sealed class QueueTimesHealthCheckOptions
{
    public const string SectionName = "HealthChecks:QueueTimes";

    public int SourceParkId { get; init; }
}
