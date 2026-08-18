using Microsoft.Extensions.Logging;

namespace Disney.Application;

public sealed class QueueCollectionJob(
    IParkReader parks,
    IQueueCollectionService collectionService,
    ILogger<QueueCollectionJob> logger) : IQueueCollectionJob
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var parkList = await parks.GetAllAsync(cancellationToken);

        foreach (var park in parkList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (park.SourceParkId <= 0)
            {
                logger.LogWarning("Park {ParkId} ({ParkName}) has no valid source id.", park.Id, park.Name);
                continue;
            }

            try
            {
                await collectionService.CollectAsync(park, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Collection failed for park {ParkId} ({ParkName}).", park.Id, park.Name);
            }
        }
    }
}
