using Dapper;
using Disney.Application;

namespace Disney.Infrastructure;

internal sealed class PostgreSqlQueuePredictionReader(
    PostgreSqlConnectionFactory connectionFactory) : IQueuePredictionReader
{
    public async Task<QueuePredictionData?> GetPredictionDataAsync(
        long parkId,
        long attractionId,
        DateTimeOffset targetAt,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            """
            SELECT attraction.id AS AttractionId,
                   attraction.name AS AttractionName
            FROM public.attractions attraction
            JOIN public.parks park
              ON park.id = attraction.park_id
            WHERE park.id = @ParkId
              AND park.is_active
              AND attraction.id = @AttractionId
              AND attraction.is_active;

            WITH target_context AS (
                SELECT EXTRACT(
                           DOW FROM @TargetAt AT TIME ZONE park.timezone
                       )::smallint AS target_day_of_week,
                       (
                           (
                               EXTRACT(HOUR FROM @TargetAt AT TIME ZONE park.timezone)::int
                               * 60
                               + EXTRACT(
                                   MINUTE FROM @TargetAt AT TIME ZONE park.timezone
                               )::int
                           ) / 15 * 15
                       )::smallint AS target_slot_minutes
                FROM public.parks park
                WHERE park.id = @ParkId
                  AND park.is_active
            )
            SELECT observation.wait_minutes AS WaitMinutes
            FROM public.queue_observations observation
            CROSS JOIN target_context target
            JOIN public.attractions attraction
              ON attraction.id = observation.attraction_id
            WHERE observation.park_id = @ParkId
              AND observation.attraction_id = @AttractionId
              AND attraction.is_active
              AND observation.is_valid
              AND observation.is_open
              AND observation.wait_minutes IS NOT NULL
              AND observation.observed_at >= @WindowStart
              AND observation.observed_at < @WindowEnd
              AND observation.observed_day_of_week = target.target_day_of_week
              AND (observation.observed_slot_minutes / 15) * 15
                  = target.target_slot_minutes
            ORDER BY observation.observed_at;
            """,
            new
            {
                ParkId = parkId,
                AttractionId = attractionId,
                TargetAt = targetAt,
                WindowStart = windowStart,
                WindowEnd = windowEnd
            },
            cancellationToken: cancellationToken));

        var attraction = await results.ReadSingleOrDefaultAsync<AttractionPredictionRow>();
        if (attraction is null)
        {
            return null;
        }

        var samples = (await results.ReadAsync<PredictionSampleRow>())
            .Select(sample => sample.WaitMinutes)
            .ToArray();
        return new QueuePredictionData(
            attraction.AttractionId,
            attraction.AttractionName,
            samples);
    }

    private sealed record AttractionPredictionRow(
        long AttractionId,
        string AttractionName);

    private sealed record PredictionSampleRow(short WaitMinutes);
}
