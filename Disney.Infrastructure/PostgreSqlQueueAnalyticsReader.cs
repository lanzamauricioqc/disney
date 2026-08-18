using Dapper;
using Disney.Application;

namespace Disney.Infrastructure;

internal sealed class PostgreSqlQueueAnalyticsReader(
    PostgreSqlConnectionFactory connectionFactory) : IQueueAnalyticsReader
{
    public async Task<IReadOnlyList<CurrentWaitTime>> GetCurrentWaitTimesAsync(
        long parkId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<CurrentWaitTime>(new CommandDefinition(
            """
            SELECT DISTINCT ON (qo.attraction_id)
                   qo.attraction_id AS AttractionId,
                   a.name AS AttractionName,
                   qo.land_id AS LandId,
                   l.name AS LandName,
                   qo.observed_at AS ObservedAt,
                   qo.is_open AS IsOpen,
                   qo.wait_minutes AS WaitMinutes
            FROM public.queue_observations qo
            JOIN public.attractions a ON a.id = qo.attraction_id
            LEFT JOIN public.lands l ON l.id = qo.land_id
            WHERE qo.park_id = @ParkId
              AND a.is_active
              AND qo.observed_at >= @From
              AND qo.observed_at <= @To
            ORDER BY qo.attraction_id, qo.observed_at DESC;
            """,
            new { ParkId = parkId, From = from, To = to },
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<WeekdayWaitTimePattern>> GetWeekdayWaitTimePatternsAsync(
        long parkId,
        long? attractionId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<WeekdayWaitTimePattern>(new CommandDefinition(
            """
            SELECT qo.attraction_id AS AttractionId,
                   a.name AS AttractionName,
                   qo.observed_day_of_week::int AS DayOfWeek,
                   qo.observed_local_hour AS LocalHour,
                   ROUND(AVG(qo.wait_minutes), 2) AS AverageWaitMinutes,
                   ROUND(
                       percentile_cont(0.5) WITHIN GROUP (ORDER BY qo.wait_minutes)::numeric,
                       2) AS MedianWaitMinutes,
                   MIN(qo.wait_minutes) AS MinimumWaitMinutes,
                   MAX(qo.wait_minutes) AS MaximumWaitMinutes,
                   COUNT(*)::int AS ObservationCount
            FROM public.queue_observations qo
            JOIN public.attractions a ON a.id = qo.attraction_id
            WHERE qo.park_id = @ParkId
              AND a.is_active
              AND qo.observed_at >= @From
              AND qo.observed_at <= @To
              AND qo.is_open
              AND qo.wait_minutes IS NOT NULL
              AND (@AttractionId IS NULL OR qo.attraction_id = @AttractionId)
            GROUP BY qo.attraction_id, a.name,
                     qo.observed_day_of_week, qo.observed_local_hour
            ORDER BY a.name, qo.observed_day_of_week, qo.observed_local_hour;
            """,
            new
            {
                ParkId = parkId,
                AttractionId = attractionId,
                From = from,
                To = to
            },
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<WeekdayClosurePattern>> GetWeekdayClosurePatternsAsync(
        long parkId,
        long? attractionId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<WeekdayClosurePattern>(new CommandDefinition(
            """
            SELECT qo.attraction_id AS AttractionId,
                   a.name AS AttractionName,
                   qo.observed_day_of_week::int AS DayOfWeek,
                   qo.observed_local_hour AS LocalHour,
                   COUNT(*) FILTER (WHERE NOT qo.is_open)::int AS ClosedObservationCount,
                   COUNT(*)::int AS TotalObservationCount,
                   ROUND(
                       COUNT(*) FILTER (WHERE NOT qo.is_open) * 100.0 / COUNT(*),
                       2) AS ClosedPercentage
            FROM public.queue_observations qo
            JOIN public.attractions a ON a.id = qo.attraction_id
            WHERE qo.park_id = @ParkId
              AND a.is_active
              AND qo.observed_at >= @From
              AND qo.observed_at <= @To
              AND (@AttractionId IS NULL OR qo.attraction_id = @AttractionId)
            GROUP BY qo.attraction_id, a.name,
                     qo.observed_day_of_week, qo.observed_local_hour
            HAVING COUNT(*) FILTER (WHERE NOT qo.is_open) > 0
            ORDER BY a.name, qo.observed_day_of_week, qo.observed_local_hour;
            """,
            new
            {
                ParkId = parkId,
                AttractionId = attractionId,
                From = from,
                To = to
            },
            cancellationToken: cancellationToken));
        return rows.AsList();
    }
}
