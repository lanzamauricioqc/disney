using System.Diagnostics;
using Repositories;

namespace WorkerModels;

internal sealed class QueueCollectionJob(
    IParksRepository parksRepository,
    IQueueTimesCollector collector,
    ILogger<QueueCollectionJob> logger) : IQueueCollectionJob
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var parks = parksRepository.GetAll();
        var succeeded = 0;
        var failed = 0;
        var skipped = 0;
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation(
            LogEvents.CollectionJobStarted,
            "Park collection job started for {ParkCount} parks.",
            parks.Count);

        foreach (var park in parks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (park.SourceParkId == 0)
            {
                skipped++;
                logger.LogWarning(
                    LogEvents.ParkSkipped,
                    "Park {ParkId} ({ParkName}) has no source park id and was skipped.",
                    park.Id,
                    park.Name);
                continue;
            }

            try
            {
                await collector.CollectAsync(park, cancellationToken);
                succeeded++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(
                    LogEvents.CollectionJobCanceled,
                    "Park collection job was canceled while processing park {ParkId} ({ParkName}).",
                    park.Id,
                    park.Name);
                throw;
            }
            catch (Exception ex)
            {
                failed++;
                logger.LogError(
                    LogEvents.ParkCollectionFailed,
                    ex,
                    "Park {ParkId} ({ParkName}) collection failed with {ExceptionType}; continuing with the next park. Error: {ErrorMessage}",
                    park.Id,
                    park.Name,
                    ex.GetType().Name,
                    ex.Message);
            }
        }

        logger.LogInformation(
            LogEvents.CollectionJobCompleted,
            "Park collection job completed in {ElapsedMs} ms. Succeeded: {SucceededCount}; failed: {FailedCount}; skipped: {SkippedCount}.",
            stopwatch.ElapsedMilliseconds,
            succeeded,
            failed,
            skipped);
    }
}
