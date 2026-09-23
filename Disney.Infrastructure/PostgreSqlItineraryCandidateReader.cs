using Dapper;
using Disney.Application;

namespace Disney.Infrastructure;

internal sealed class PostgreSqlItineraryCandidateReader(
    PostgreSqlConnectionFactory connectionFactory) : IItineraryCandidateReader
{
    public async Task<IReadOnlyList<ItineraryCandidate>> GetCandidatesAsync(
        long parkId,
        IReadOnlyCollection<long> attractionIds,
        DateTimeOffset liveObservationFrom,
        DateTimeOffset liveObservationTo,
        DateTimeOffset historicalTargetAt,
        DateTimeOffset historicalWindowStart,
        DateTimeOffset historicalWindowEnd,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var candidates = await connection.QueryAsync<ItineraryCandidate>(
            new CommandDefinition(
                """
                SELECT attraction.id AS AttractionId,
                       attraction.name AS AttractionName,
                       attraction.is_active AS IsActive,
                       attraction.duration_minutes::integer AS DurationMinutes,
                       latest_observation.is_open AS IsOpen,
                       latest_observation.wait_minutes AS WaitMinutes,
                       historical_wait.wait_minutes AS HistoricalWaitMinutes
                FROM public.attractions attraction
                JOIN public.parks park
                  ON park.id = attraction.park_id
                LEFT JOIN LATERAL (
                    SELECT observation.is_open,
                           observation.wait_minutes
                    FROM public.queue_observations observation
                    WHERE observation.attraction_id = attraction.id
                      AND observation.park_id = park.id
                      AND observation.is_valid
                      AND observation.observed_at >= @LiveObservationFrom
                      AND observation.observed_at <= @LiveObservationTo
                    ORDER BY observation.observed_at DESC
                    LIMIT 1
                ) latest_observation ON TRUE
                LEFT JOIN LATERAL (
                    SELECT CASE
                               WHEN COUNT(*) >= @MinimumHistoricalSampleCount
                               THEN percentile_cont(0.5) WITHIN GROUP (
                                   ORDER BY observation.wait_minutes
                               )::smallint
                           END AS wait_minutes
                    FROM public.queue_observations observation
                    WHERE observation.attraction_id = attraction.id
                      AND observation.park_id = park.id
                      AND observation.is_valid
                      AND observation.is_open
                      AND observation.wait_minutes IS NOT NULL
                      AND observation.observed_at >= @HistoricalWindowStart
                      AND observation.observed_at < @HistoricalWindowEnd
                      AND observation.observed_day_of_week = EXTRACT(
                          DOW FROM @HistoricalTargetAt AT TIME ZONE park.timezone
                      )::smallint
                      AND (observation.observed_slot_minutes / 15) * 15 = (
                          (
                              EXTRACT(
                                  HOUR FROM @HistoricalTargetAt AT TIME ZONE park.timezone
                              )::int * 60
                              + EXTRACT(
                                  MINUTE FROM @HistoricalTargetAt AT TIME ZONE park.timezone
                              )::int
                          ) / 15 * 15
                      )
                ) historical_wait ON TRUE
                WHERE park.id = @ParkId
                  AND park.is_active
                  AND attraction.id = ANY(@AttractionIds)
                ORDER BY attraction.id;
                """,
                new
                {
                    ParkId = parkId,
                    AttractionIds = attractionIds.ToArray(),
                    LiveObservationFrom = liveObservationFrom,
                    LiveObservationTo = liveObservationTo,
                    HistoricalTargetAt = historicalTargetAt,
                    HistoricalWindowStart = historicalWindowStart,
                    HistoricalWindowEnd = historicalWindowEnd,
                    MinimumHistoricalSampleCount = QueueHistoryWindow.MinimumSampleCount
                },
                cancellationToken: cancellationToken));
        return candidates.AsList();
    }
}
