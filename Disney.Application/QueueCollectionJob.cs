using Microsoft.Extensions.Logging;

namespace Disney.Application;

public sealed class QueueCollectionJob(
    IParkReader parkReader,
    IQueueCollectionService collectionService,
    ILogger<QueueCollectionJob> logger) : IQueueCollectionJob
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var parks = await parkReader.GetAllAsync(cancellationToken);

        foreach (var park in parks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!park.CollectionEnabled)
            {
                continue;
            }

            if (park.SourceParkId <= 0)
            {
                logger.LogWarning("Park {ParkId} ({ParkName}) has no valid source id.", park.Id, park.Name);
                continue;
            }

            if (park.LastCollectionStartedAt is not null &&
                park.LastCollectionStartedAt.Value.AddMinutes(
                    park.CollectionIntervalMinutes) > DateTimeOffset.UtcNow)
            {
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
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Collection failed for park {ParkId} ({ParkName}).",
                    park.Id,
                    park.Name);
            }
        }
    }
}
