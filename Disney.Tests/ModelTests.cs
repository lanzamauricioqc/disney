using Repositories;
using WorkerModels;

namespace Disney.Tests;

public sealed class ModelTests
{
    [Fact]
    public void DomainAndTransportModels_ExposeTheirData()
    {
        var now = DateTimeOffset.UtcNow;
        var park = new Park
        {
            Id = 1,
            SourceParkId = 6,
            Name = "Magic Kingdom",
            Timezone = "UTC",
            CreatedAt = now,
            UpdatedAt = now
        };
        var land = new Repositories.Land
        {
            Id = 2,
            ParkId = park.Id,
            SourceLandId = 10,
            Name = "Tomorrowland",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        var attraction = new Attraction
        {
            Id = 3,
            ParkId = park.Id,
            CurrentLandId = land.Id,
            SourceRideId = 20,
            Name = "Space Mountain",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        var run = new QueueCollectionRun
        {
            Id = 4,
            ParkId = park.Id,
            StartedAt = now,
            CompletedAt = now,
            Success = true,
            ErrorMessage = "none"
        };
        var observation = new QueueObservation
        {
            Id = 5,
            CollectionRunId = run.Id,
            ParkId = park.Id,
            LandId = land.Id,
            AttractionId = attraction.Id,
            CollectedAt = now,
            ObservedLocalDate = DateOnly.FromDateTime(now.DateTime),
            ObservedLocalTime = TimeOnly.FromDateTime(now.DateTime),
            ObservedLocalHour = now.Hour,
            ObservedSlotMinutes = now.Minute,
            ObservedDayOfWeek = (int)now.DayOfWeek,
            IsOpen = true,
            WaitMinutes = 10,
            SourceLastUpdated = now,
            CreatedAt = now
        };
        var ride = new Ride
        {
            Id = attraction.SourceRideId,
            Name = attraction.Name,
            IsOpen = observation.IsOpen,
            WaitTime = observation.WaitMinutes.Value,
            LastUpdated = now
        };
        var transportLand = new WorkerModels.Land
        {
            Id = land.SourceLandId,
            Name = land.Name,
            Rides = [ride]
        };
        var waitingTimes = new WaitingTimeModel
        {
            Lands = [transportLand],
            Rides = [ride]
        };
        var options = new QueueCollectionOptions
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        Assert.Equal("Magic Kingdom", park.Name);
        Assert.Equal(park.Id, land.ParkId);
        Assert.Equal(land.Id, attraction.CurrentLandId);
        Assert.True(run.Success);
        Assert.Equal(10, observation.WaitMinutes);
        Assert.Single(waitingTimes.Lands);
        Assert.Single(waitingTimes.Rides);
        Assert.Single(transportLand.Rides);
        Assert.Equal("Space Mountain", ride.Name);
        Assert.Equal(TimeSpan.FromSeconds(1), options.Interval);
    }
}
