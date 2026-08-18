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
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                logger.LogInformation("Starting queue-time collection cycle.");
                using var scope = scopeFactory.CreateScope();
                var job = scope.ServiceProvider.GetRequiredService<IQueueCollectionJob>();
                await job.ExecuteAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Queue-time collection cycle failed.");
            }

            await Task.Delay(_collectionInterval, stoppingToken);
        }
    }
}