using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace WorkerModels;

public sealed class Worker(
    ILogger<Worker> logger,
    IServiceScopeFactory scopeFactory,
    IOptions<QueueCollectionOptions> options) : BackgroundService
{
    private readonly TimeSpan _collectionInterval = options.Value.Interval;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            LogEvents.WorkerStarted,
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
                    LogEvents.CollectionCycleStarted,
                    "Queue-time collection cycle started.");

                using var serviceScope = scopeFactory.CreateScope();
                var job = serviceScope.ServiceProvider.GetRequiredService<IQueueCollectionJob>();
                await job.ExecuteAsync(stoppingToken);

                logger.LogInformation(
                    LogEvents.CollectionCycleCompleted,
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
                    LogEvents.CollectionCycleFailed,
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
            LogEvents.WorkerStopping,
            "Queue-time worker is stopping.");
    }
}