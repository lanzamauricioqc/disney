using Repositories;

namespace WorkerModels;

internal sealed class QueueObservationFactory
{
    public QueueObservation Create(
        int collectionRunId,
        Park park,
        int? landId,
        int attractionId,
        Ride ride,
        DateTimeOffset collectedAt)
    {
        DateTimeOffset observedLocal;

        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(park.Timezone);
            observedLocal = TimeZoneInfo.ConvertTime(ride.LastUpdated, timeZone);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new InvalidOperationException(
                $"Park {park.Id} has invalid timezone '{park.Timezone}'.",
                ex);
        }

        return new QueueObservation
        {
            CollectionRunId = collectionRunId,
            ParkId = park.Id,
            LandId = landId,
            AttractionId = attractionId,
            CollectedAt = collectedAt,
            ObservedLocalDate = DateOnly.FromDateTime(observedLocal.DateTime),
            ObservedLocalTime = TimeOnly.FromDateTime(observedLocal.DateTime),
            ObservedLocalHour = observedLocal.Hour,
            ObservedSlotMinutes = observedLocal.Minute,
            ObservedDayOfWeek = (int)observedLocal.DayOfWeek,
            IsOpen = ride.IsOpen,
            WaitMinutes = ride.IsOpen ? ride.WaitTime : null,
            SourceLastUpdated = ride.LastUpdated,
            CreatedAt = collectedAt
        };
    }
}
