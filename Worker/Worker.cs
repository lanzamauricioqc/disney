using System.Diagnostics;
using Disney.Application;
using Microsoft.Extensions.Options;

namespace Disney.Worker;

public sealed class QueueCollectionWorker(
    ILogger<QueueCollectionWorker> logger,
    IServiceScopeFactory scopeFactory,
    IOptions<QueueCollectionOptions> options) : BackgroundService
{
    private readonly TimeSpan _collectionInterval = options.Value.Interval;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Queue-time worker started with a collection interval of {CollectionIntervalMs} ms.",
            _collectionInterval.TotalMilliseconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            var cycleId = Guid.NewGuid();
            var stopwatch = Stopwatch.StartNew();
            using var scope = logger.BeginScope(
                new Dictionary<string, object> { ["CollectionCycleId"] = cycleId });

            try
            {
                logger.LogInformation(
                    "Queue-time collection cycle started.");

                using var serviceScope = scopeFactory.CreateScope();
                var job = serviceScope.ServiceProvider.GetRequiredService<IQueueCollectionJob>();
                await job.ExecuteAsync(stoppingToken);

                logger.LogInformation(
                    "Queue-time collection cycle completed in {ElapsedMs} ms.",
                    stopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Queue-time collection cycle failed after {ElapsedMs} ms.",
                    stopwatch.ElapsedMilliseconds);
            }

            try
            {
                await Task.Delay(_collectionInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        logger.LogInformation(
                "Queue-time worker is stopping.");
    }
}