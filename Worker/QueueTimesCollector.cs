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

        try
        {
            var queueTimes = await queueTimesProvider.GetQueueTimesForParkAsync(
                park.SourceParkId,
                cancellationToken);

            ProcessQueueTimes(run.Id, park, queueTimes);
            runsRepository.Complete(run.Id, DateTimeOffset.UtcNow, success: true);
        }
        catch (Exception ex)
        {
            runsRepository.Complete(
                run.Id,
                DateTimeOffset.UtcNow,
                success: false,
                errorMessage: ex.Message);
            throw;
        }
    }

    private void ProcessQueueTimes(
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
        }

        foreach (var attraction in attractionsBySourceId.Values.Where(
                     attraction => attraction.IsActive &&
                                   !processedRideIds.Contains(attraction.SourceRideId)))
        {
            attraction.IsActive = false;
            attractionsRepository.Upsert(attraction);
        }
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

        logger.LogInformation(
            "{RideId} | {RideName} | Open: {IsOpen} | Wait: {WaitTime} min | Updated: {LastUpdated}",
            ride.Id,
            ride.Name,
            ride.IsOpen,
            ride.WaitTime,
            ride.LastUpdated);
    }
}
