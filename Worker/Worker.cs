using System.Linq;
using Repositories;

namespace WorkerModels
{
    public class Worker(
        IQueueTimesProvider queueTimesProvider,
        ILogger<Worker> logger,
        IServiceScopeFactory scopeFactory) : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                }

                // load parks within a scope
                List<Park> parks;
                using (var scope = _scopeFactory.CreateScope())
                {
                    var parksRepo = scope.ServiceProvider.GetRequiredService<IParksRepository>();
                    parks = parksRepo.GetAll().ToList();
                }

                foreach (var park in parks)
                {
                    if (park.SourceParkId == 0)
                    {
                        logger.LogWarning("Park {ParkId} has no source id, skipping.", park.Id);
                        continue;
                    }

                    var queueTimes = await queueTimesProvider.GetQueueTimesForParkAsync(park.SourceParkId, stoppingToken);

                    if (queueTimes is not null)
                    {
                        // create a scope per park for repository operations
                        using var scope = _scopeFactory.CreateScope();
                        var landsRepo = scope.ServiceProvider.GetRequiredService<ILandsRepository>();
                        var attractionsRepo = scope.ServiceProvider.GetRequiredService<IAttractionsRepository>();
                        var observationsRepo = scope.ServiceProvider.GetRequiredService<IQueueObservationsRepository>();
                        var runsRepo = scope.ServiceProvider.GetRequiredService<IQueueCollectionRunsRepository>();

                        await ProcessQueueTimesAsync(park, queueTimes, landsRepo, attractionsRepo, observationsRepo, runsRepo);
                    }
                }

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
        
        private async Task ProcessQueueTimesAsync(
            Park park,
            WaitingTimeModel queueTimes,
            ILandsRepository landsRepository,
            IAttractionsRepository attractionsRepository,
            IQueueObservationsRepository observationsRepository,
            IQueueCollectionRunsRepository runsRepository)
        {
            // record run start
            var run = new QueueCollectionRun
            {
                Id = 0,
                ParkId = park.Id,
                StartedAt = DateTimeOffset.UtcNow
            };

            run = runsRepository.InsertOrUpdate(run);

            var existingLands = landsRepository.GetAll().Where(l => l.ParkId == park.Id).ToList();
            var existingAttractions = attractionsRepository.GetAll().Where(a => a.ParkId == park.Id).ToList();

            foreach (var landModel in queueTimes.Lands)
            {
                logger.LogInformation("Land: {LandName}", landModel.Name);

                var sourceLandId = landModel.Id;
                var existingLand = existingLands.FirstOrDefault(l => l.SourceLandId == sourceLandId);

                var landEntity = new Repositories.Land
                {
                    Id = existingLand?.Id ?? 0,
                    ParkId = park.Id,
                    SourceLandId = sourceLandId,
                    Name = landModel.Name,
                    IsActive = true,
                    CreatedAt = existingLand?.CreatedAt ?? default,
                    UpdatedAt = DateTimeOffset.UtcNow
                };

                landEntity = landsRepository.InsertOrUpdate(landEntity);

                // refresh attractions list for this park
                existingAttractions = attractionsRepository.GetAll().Where(a => a.ParkId == park.Id).ToList();

                foreach (var ride in landModel.Rides)
                {
                    logger.LogInformation(
                        "{RideId} | {RideName} | Open: {IsOpen} | Wait: {WaitTime} min | Updated: {LastUpdated}",
                        ride.Id,
                        ride.Name,
                        ride.IsOpen,
                        ride.WaitTime,
                        ride.LastUpdated);

                    var sourceRideId = ride.Id;
                    var existingAttraction = existingAttractions.FirstOrDefault(a => a.SourceRideId == sourceRideId);

                    var attractionEntity = new Attraction
                    {
                        Id = existingAttraction?.Id ?? 0,
                        ParkId = park.Id,
                        CurrentLandId = landEntity.Id,
                        SourceRideId = sourceRideId,
                        Name = ride.Name,
                        IsActive = true,
                        CreatedAt = existingAttraction?.CreatedAt ?? default,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };

                    attractionEntity = attractionsRepository.InsertOrUpdate(attractionEntity);

                    // build observation
                    var observed = ride.LastUpdated;
                    DateTimeOffset observedLocal;
                    try
                    {
                        var tz = TimeZoneInfo.FindSystemTimeZoneById(park.Timezone);
                        observedLocal = TimeZoneInfo.ConvertTime(observed, tz);
                    }
                    catch
                    {
                        observedLocal = observed.ToLocalTime();
                    }

                    var observation = new QueueObservation
                    {
                        Id = 0,
                        CollectionRunId = run.Id,
                        ParkId = park.Id,
                        LandId = landEntity.Id,
                        AttractionId = attractionEntity.Id,
                        CollectedAt = DateTimeOffset.UtcNow,
                        ObservedLocalDate = DateOnly.FromDateTime(observedLocal.DateTime),
                        ObservedLocalTime = TimeOnly.FromDateTime(observedLocal.DateTime),
                        ObservedLocalHour = observedLocal.Hour,
                        ObservedSlotMinutes = observedLocal.Minute,
                        ObservedDayOfWeek = (int)observedLocal.DayOfWeek,
                        IsOpen = ride.IsOpen,
                        WaitMinutes = ride.IsOpen ? ride.WaitTime : null,
                        SourceLastUpdated = ride.LastUpdated,
                        CreatedAt = DateTimeOffset.UtcNow
                    };

                    observationsRepository.InsertOrUpdate(observation);
                }
            }

            run.CompletedAt = DateTimeOffset.UtcNow;
            run.Success = true;
            runsRepository.InsertOrUpdate(run);

            await Task.CompletedTask;
        }
    }
}