using System.Diagnostics;
using Disney.Domain;
using Microsoft.Extensions.Logging;

namespace Disney.Application;

public sealed class QueueCollectionService(
    IQueueTimesProvider queueTimesProvider,
    IQueueCollectionStore store,
    ILogger<QueueCollectionService> logger) : IQueueCollectionService
{
    public async Task<CollectionResult> CollectAsync(
        Park park,
        CancellationToken cancellationToken)
    {
        var runId = await store.StartRunAsync(
            park.Id,
            DateTimeOffset.UtcNow,
            cancellationToken);
        var stopwatch = Stopwatch.StartNew();

        using var scope = logger.BeginScope(new Dictionary<string, object>
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
            var result = await store.PersistSuccessfulRunAsync(
                runId,
                park,
                snapshot,
                DateTimeOffset.UtcNow,
                cancellationToken);

            logger.LogInformation(
                "Collection run completed in {ElapsedMs} ms with {ObservationCount} new observations.",
                stopwatch.ElapsedMilliseconds,
                result.ObservationCount);
            return result;
        }
        catch (Exception ex)
        {
            await store.FailRunAsync(
                runId,
                DateTimeOffset.UtcNow,
                ex.Message,
                CancellationToken.None);
            logger.LogError(ex, "Collection run failed after {ElapsedMs} ms.", stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
