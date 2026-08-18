using Dapper;
using Disney.Application;

namespace Disney.Infrastructure;

internal sealed class PostgreSqlQueueHistoryReader(
    PostgreSqlConnectionFactory connectionFactory) : IQueueHistoryReader
{
    public async Task<IReadOnlyList<QueueHistoryPoint>> GetHistoryAsync(
        QueueHistoryQuery query,
        CancellationToken cancellationToken)
    {
        Validate(query);
        await using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<QueueHistoryPoint>(new CommandDefinition(
            """
            SELECT qo.attraction_id AS AttractionId, a.name AS AttractionName,
                   qo.observed_at AS ObservedAt, qo.is_open AS IsOpen,
                   qo.wait_minutes AS WaitMinutes
            FROM public.queue_observations qo
            JOIN public.attractions a ON a.id = qo.attraction_id
            WHERE qo.park_id = @ParkId
              AND qo.observed_at >= @From AND qo.observed_at < @To
              AND (@AttractionId IS NULL OR qo.attraction_id = @AttractionId)
            ORDER BY qo.observed_at, qo.attraction_id
            LIMIT @Limit;
            """,
            query,
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<QueueHourlySummary>> GetHourlySummaryAsync(
        QueueHistoryQuery query,
        CancellationToken cancellationToken)
    {
        Validate(query);
        await using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<QueueHourlySummary>(new CommandDefinition(
            """
            SELECT qo.attraction_id AS AttractionId, a.name AS AttractionName,
                   qo.observed_local_date AS LocalDate,
                   qo.observed_local_hour AS LocalHour,
                   AVG(qo.wait_minutes)::numeric AS AverageWaitMinutes,
                   MAX(qo.wait_minutes) AS MaximumWaitMinutes,
                   COUNT(qo.wait_minutes)::int AS ObservationCount
            FROM public.queue_observations qo
            JOIN public.attractions a ON a.id = qo.attraction_id
            WHERE qo.park_id = @ParkId
              AND qo.observed_at >= @From AND qo.observed_at < @To
              AND qo.is_open AND qo.wait_minutes IS NOT NULL
              AND (@AttractionId IS NULL OR qo.attraction_id = @AttractionId)
            GROUP BY qo.attraction_id, a.name,
                     qo.observed_local_date, qo.observed_local_hour
            ORDER BY qo.observed_local_date, qo.observed_local_hour, a.name
            LIMIT @Limit;
            """,
            query,
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    private static void Validate(QueueHistoryQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.To <= query.From)
        {
            throw new ArgumentException("The query end must be later than its start.", nameof(query));
        }

        if (query.Limit is < 1 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "Limit must be between 1 and 10000.");
        }
    }
}
