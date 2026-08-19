using System.Diagnostics;
using Disney.Domain;
using Microsoft.Extensions.Logging;

namespace Disney.Application;

public sealed class QueueCollectionService(
    IQueueTimesProvider queueTimesProvider,
    IQueueCollectionStore collectionStore,
    ILogger<QueueCollectionService> logger) : IQueueCollectionService
{
    public async Task<CollectionResult> CollectAsync(
        Park park,
        CancellationToken cancellationToken)
    {
        var runId = await collectionStore.StartRunAsync(
            park.Id,
            DateTimeOffset.UtcNow,
            cancellationToken);
        var stopwatch = Stopwatch.StartNew();

        using var loggingScope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CollectionRunId"] = runId,
            ["ParkId"] = park.Id,
            ["SourceParkId"] = park.SourceParkId
        });

        try
        {
            var snapshot = await queueTimesProvider.GetQueueTimesForParkAsync(
                park.SourceParkId,
                cancellationToken);
            var collectionResult = await collectionStore.PersistSuccessfulRunAsync(
                runId,
                park,
                snapshot,
                DateTimeOffset.UtcNow,
                cancellationToken);

            logger.LogInformation(
                "Collection run completed in {ElapsedMilliseconds} ms with {ObservationCount} new observations.",
                stopwatch.ElapsedMilliseconds,
                collectionResult.ObservationCount);
            return collectionResult;
        }
        catch (Exception exception)
        {
            await collectionStore.FailRunAsync(
                runId,
                DateTimeOffset.UtcNow,
                exception.Message,
                CancellationToken.None);
            logger.LogError(
                exception,
                "Collection run failed after {ElapsedMilliseconds} ms.",
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
