using Dapper;
using Disney.Application;

namespace Disney.Infrastructure;

internal sealed class PostgreSqlQueueAnalyticsReader(
    PostgreSqlConnectionFactory connectionFactory) : IQueueAnalyticsReader
{
    public async Task<IReadOnlyList<CurrentWaitTime>> GetCurrentWaitTimesAsync(
        long parkId,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var currentWaitTimes = await connection.QueryAsync<CurrentWaitTime>(new CommandDefinition(
            """
            SELECT DISTINCT ON (observation.attraction_id)
                   observation.attraction_id AS AttractionId,
                   attraction.name AS AttractionName,
                   observation.land_id AS LandId,
                   land.name AS LandName,
                   observation.observed_at AS ObservedAt,
                   observation.is_open AS IsOpen,
                   observation.wait_minutes AS WaitMinutes
            FROM public.queue_observations observation
            JOIN public.attractions attraction
              ON attraction.id = observation.attraction_id
            LEFT JOIN public.lands land
              ON land.id = observation.land_id
            WHERE observation.park_id = @ParkId
              AND attraction.is_active
              AND observation.observed_at >= @WindowStart
              AND observation.observed_at <= @WindowEnd
            ORDER BY observation.attraction_id, observation.observed_at DESC;
            """,
            new
            {
                ParkId = parkId,
                WindowStart = windowStart,
                WindowEnd = windowEnd
            },
            cancellationToken: cancellationToken));
        return currentWaitTimes.AsList();
    }

    public async Task<IReadOnlyList<WeekdayWaitTimePattern>> GetWeekdayWaitTimePatternsAsync(
        long parkId,
        long? attractionId,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var waitTimePatterns =
            await connection.QueryAsync<WeekdayWaitTimePattern>(new CommandDefinition(
            """
            SELECT observation.attraction_id AS AttractionId,
                   attraction.name AS AttractionName,
                   observation.observed_day_of_week::int AS DayOfWeek,
                   observation.observed_local_hour AS LocalHour,
                   ROUND(AVG(observation.wait_minutes), 2) AS AverageWaitMinutes,
                   ROUND(
                       percentile_cont(0.5)
                           WITHIN GROUP (ORDER BY observation.wait_minutes)::numeric,
                       2) AS MedianWaitMinutes,
                   MIN(observation.wait_minutes) AS MinimumWaitMinutes,
                   MAX(observation.wait_minutes) AS MaximumWaitMinutes,
                   COUNT(*)::int AS ObservationCount
            FROM public.queue_observations observation
            JOIN public.attractions attraction
              ON attraction.id = observation.attraction_id
            WHERE observation.park_id = @ParkId
              AND attraction.is_active
              AND observation.observed_at >= @WindowStart
              AND observation.observed_at <= @WindowEnd
              AND observation.is_open
              AND observation.wait_minutes IS NOT NULL
              AND (@AttractionId IS NULL OR observation.attraction_id = @AttractionId)
            GROUP BY observation.attraction_id, attraction.name,
                     observation.observed_day_of_week, observation.observed_local_hour
            ORDER BY attraction.name, observation.observed_day_of_week,
                     observation.observed_local_hour;
            """,
            new
            {
                ParkId = parkId,
                AttractionId = attractionId,
                WindowStart = windowStart,
                WindowEnd = windowEnd
            },
            cancellationToken: cancellationToken));
        return waitTimePatterns.AsList();
    }

    public async Task<IReadOnlyList<WeekdayClosurePattern>> GetWeekdayClosurePatternsAsync(
        long parkId,
        long? attractionId,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var closurePatterns =
            await connection.QueryAsync<WeekdayClosurePattern>(new CommandDefinition(
            """
            SELECT observation.attraction_id AS AttractionId,
                   attraction.name AS AttractionName,
                   observation.observed_day_of_week::int AS DayOfWeek,
                   observation.observed_local_hour AS LocalHour,
                   COUNT(*) FILTER (WHERE NOT observation.is_open)::int
                       AS ClosedObservationCount,
                   COUNT(*)::int AS TotalObservationCount,
                   ROUND(
                       COUNT(*) FILTER (WHERE NOT observation.is_open) * 100.0 / COUNT(*),
                       2) AS ClosedPercentage
            FROM public.queue_observations observation
            JOIN public.attractions attraction
              ON attraction.id = observation.attraction_id
            WHERE observation.park_id = @ParkId
              AND attraction.is_active
              AND observation.observed_at >= @WindowStart
              AND observation.observed_at <= @WindowEnd
              AND (@AttractionId IS NULL OR observation.attraction_id = @AttractionId)
            GROUP BY observation.attraction_id, attraction.name,
                     observation.observed_day_of_week, observation.observed_local_hour
            HAVING COUNT(*) FILTER (WHERE NOT observation.is_open) > 0
            ORDER BY attraction.name, observation.observed_day_of_week,
                     observation.observed_local_hour;
            """,
            new
            {
                ParkId = parkId,
                AttractionId = attractionId,
                WindowStart = windowStart,
                WindowEnd = windowEnd
            },
            cancellationToken: cancellationToken));
        return closurePatterns.AsList();
    }
}
