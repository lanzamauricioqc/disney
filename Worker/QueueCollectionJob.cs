using Repositories;

namespace WorkerModels;

internal sealed class QueueCollectionJob(
    IParksRepository parksRepository,
    IQueueTimesCollector collector,
    ILogger<QueueCollectionJob> logger) : IQueueCollectionJob
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        foreach (var park in parksRepository.GetAll())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (park.SourceParkId == 0)
            {
                logger.LogWarning("Park {ParkId} has no source id, skipping.", park.Id);
                continue;
            }

            try
            {
                await collector.CollectAsync(park, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Queue-time collection failed for park {ParkId}.", park.Id);
            }
        }
    }
}
