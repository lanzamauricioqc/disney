using Disney.Domain;

namespace Disney.Application;

public sealed class QueueObservationFactory
{
    public QueueObservation Create(
        long collectionRunId,
        Park park,
        long? landId,
        long attractionId,
        QueueRideSnapshot ride,
        DateTimeOffset collectedAt)
    {
        var observedUtc = ride.ObservedAt.ToUniversalTime();
        DateTimeOffset observedLocal;

        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(park.Timezone);
            observedLocal = TimeZoneInfo.ConvertTime(ride.ObservedAt, timeZone);
        }
        catch (Exception exception)
            when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new InvalidOperationException(
                $"Park {park.Id} has invalid timezone '{park.Timezone}'.",
                exception);
        }

        short? waitMinutes = ride.IsOpen
            ? checked((short)ride.WaitMinutes)
            : null;

        return new QueueObservation
        {
            CollectionRunId = collectionRunId,
            ParkId = park.Id,
            LandId = landId,
            AttractionId = attractionId,
            CollectedAt = collectedAt,
            ObservedAt = ride.ObservedAt,
            ObservedUtcDate = DateOnly.FromDateTime(observedUtc.DateTime),
            ObservedUtcTime = TimeOnly.FromDateTime(observedUtc.DateTime),
            ObservedUtcHour = checked((short)observedUtc.Hour),
            ObservedUtcSlotMinutes =
                checked((short)((observedUtc.Hour * 60) + observedUtc.Minute)),
            ObservedUtcDayOfWeek = checked((short)observedUtc.DayOfWeek),
            ObservedLocalDate = DateOnly.FromDateTime(observedLocal.DateTime),
            ObservedLocalTime = TimeOnly.FromDateTime(observedLocal.DateTime),
            ObservedLocalHour = checked((short)observedLocal.Hour),
            ObservedSlotMinutes =
                checked((short)((observedLocal.Hour * 60) + observedLocal.Minute)),
            ObservedDayOfWeek = checked((short)observedLocal.DayOfWeek),
            IsOpen = ride.IsOpen,
            WaitMinutes = waitMinutes,
            CreatedAt = collectedAt
        };
    }
}
