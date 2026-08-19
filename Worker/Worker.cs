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
            "Queue-time worker started with a collection interval of {CollectionIntervalMilliseconds} ms.",
            _collectionInterval.TotalMilliseconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            var collectionCycleId = Guid.NewGuid();
            var stopwatch = Stopwatch.StartNew();
            using var loggingScope = logger.BeginScope(
                new Dictionary<string, object>
                {
                    ["CollectionCycleId"] = collectionCycleId
                });

            try
            {
                logger.LogInformation(
                    "Queue-time collection cycle started.");

                using var serviceScope = scopeFactory.CreateScope();
                var collectionJob =
                    serviceScope.ServiceProvider.GetRequiredService<IQueueCollectionJob>();
                await collectionJob.ExecuteAsync(stoppingToken);

                logger.LogInformation(
                    "Queue-time collection cycle completed in {ElapsedMilliseconds} ms.",
                    stopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Queue-time collection cycle failed after {ElapsedMilliseconds} ms.",
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