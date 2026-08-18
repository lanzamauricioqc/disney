using System.Diagnostics;
using Repositories;

namespace WorkerModels;

internal sealed class QueueTimesCollector(
    IQueueTimesProvider queueTimesProvider,
    ILandsRepository landsRepository,
    IAttractionsRepository attractionsRepository,
    IQueueObservationsRepository observationsRepository,
    IQueueCollectionRunsRepository runsRepository,
    QueueObservationFactory observationFactory,
    ILogger<QueueTimesCollector> logger) : IQueueTimesCollector
{
    public async Task CollectAsync(Park park, CancellationToken cancellationToken)
    {
        var run = runsRepository.Start(park.Id, DateTimeOffset.UtcNow);
        var stopwatch = Stopwatch.StartNew();
        using var scope = logger.BeginScope(
            new Dictionary<string, object>
            {
                ["CollectionRunId"] = run.Id,
                ["ParkId"] = park.Id,
                ["SourceParkId"] = park.SourceParkId
            });

        try
        {
            logger.LogInformation(
                LogEvents.CollectionRunStarted,
                "Collection run started for park {ParkName}.",
                park.Name);

            var queueTimes = await queueTimesProvider.GetQueueTimesForParkAsync(
                park.SourceParkId,
                cancellationToken);

            logger.LogInformation(
                LogEvents.QueueTimesReceived,
                "Queue-times payload received with {LandCount} lands and {TopLevelRideCount} top-level rides.",
                queueTimes.Lands.Count,
                queueTimes.Rides.Count);

            var result = ProcessQueueTimes(run.Id, park, queueTimes);
            runsRepository.Complete(run.Id, DateTimeOffset.UtcNow, success: true);

            logger.LogInformation(
                LogEvents.CollectionRunCompleted,
                "Collection run completed in {ElapsedMs} ms. Lands processed: {LandCount}; rides observed: {RideCount}; lands deactivated: {DeactivatedLandCount}; attractions deactivated: {DeactivatedAttractionCount}.",
                stopwatch.ElapsedMilliseconds,
                result.LandCount,
                result.RideCount,
                result.DeactivatedLandCount,
                result.DeactivatedAttractionCount);
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            runsRepository.Complete(
                run.Id,
                DateTimeOffset.UtcNow,
                success: false,
                errorMessage: ex.Message);
            logger.LogWarning(
                LogEvents.CollectionRunCanceled,
                "Collection run was canceled after {ElapsedMs} ms.",
                stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            runsRepository.Complete(
                run.Id,
                DateTimeOffset.UtcNow,
                success: false,
                errorMessage: ex.Message);
            logger.LogError(
                LogEvents.CollectionRunFailed,
                ex,
                "Collection run failed after {ElapsedMs} ms.",
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    private CollectionResult ProcessQueueTimes(
        int collectionRunId,
        Park park,
        WaitingTimeModel queueTimes)
    {
        var collectedAt = DateTimeOffset.UtcNow;
        var landsBySourceId = landsRepository.GetByParkId(park.Id)
            .ToDictionary(land => land.SourceLandId);
        var attractionsBySourceId = attractionsRepository.GetByParkId(park.Id)
            .ToDictionary(attraction => attraction.SourceRideId);
        var returnedLandIds = queueTimes.Lands
            .Select(land => land.Id)
            .ToHashSet();
        var processedRideIds = new HashSet<int>();
        var deactivatedLandCount = 0;
        var deactivatedAttractionCount = 0;

        foreach (var landModel in queueTimes.Lands)
        {
            landsBySourceId.TryGetValue(landModel.Id, out var existingLand);

            var land = landsRepository.Upsert(new Repositories.Land
            {
                Id = existingLand?.Id ?? 0,
                ParkId = park.Id,
                SourceLandId = landModel.Id,
                Name = landModel.Name,
                IsActive = true,
                CreatedAt = existingLand?.CreatedAt ?? default
            });

            landsBySourceId[land.SourceLandId] = land;

            foreach (var ride in landModel.Rides)
            {
                SaveRide(collectionRunId, park, land.Id, ride, collectedAt, attractionsBySourceId);
                processedRideIds.Add(ride.Id);
            }
        }

        foreach (var ride in queueTimes.Rides.Where(ride => processedRideIds.Add(ride.Id)))
        {
            SaveRide(collectionRunId, park, null, ride, collectedAt, attractionsBySourceId);
        }

        foreach (var land in landsBySourceId.Values.Where(
                     land => land.IsActive && !returnedLandIds.Contains(land.SourceLandId)))
        {
            land.IsActive = false;
            landsRepository.Upsert(land);
            deactivatedLandCount++;
        }

        foreach (var attraction in attractionsBySourceId.Values.Where(
                     attraction => attraction.IsActive &&
                                   !processedRideIds.Contains(attraction.SourceRideId)))
        {
            attraction.IsActive = false;
            attractionsRepository.Upsert(attraction);
            deactivatedAttractionCount++;
        }

        return new CollectionResult(
            queueTimes.Lands.Count,
            processedRideIds.Count,
            deactivatedLandCount,
            deactivatedAttractionCount);
    }

    private void SaveRide(
        int collectionRunId,
        Park park,
        int? landId,
        Ride ride,
        DateTimeOffset collectedAt,
        IDictionary<int, Attraction> attractionsBySourceId)
    {
        attractionsBySourceId.TryGetValue(ride.Id, out var existingAttraction);

        var attraction = attractionsRepository.Upsert(new Attraction
        {
            Id = existingAttraction?.Id ?? 0,
            ParkId = park.Id,
            CurrentLandId = landId,
            SourceRideId = ride.Id,
            Name = ride.Name,
            IsActive = true,
            CreatedAt = existingAttraction?.CreatedAt ?? default
        });

        attractionsBySourceId[attraction.SourceRideId] = attraction;

        var observation = observationFactory.Create(
            collectionRunId,
            park,
            landId,
            attraction.Id,
            ride,
            collectedAt);

        observationsRepository.Upsert(observation);

        logger.LogDebug(
            LogEvents.RideObserved,
            "Ride observation stored for source ride {SourceRideId} ({RideName}). Open: {IsOpen}; wait: {WaitMinutes}; source updated: {SourceLastUpdated}.",
            ride.Id,
            ride.Name,
            ride.IsOpen,
            ride.WaitTime,
            ride.LastUpdated);
    }

    private sealed record CollectionResult(
        int LandCount,
        int RideCount,
        int DeactivatedLandCount,
        int DeactivatedAttractionCount);
}
