using Dapper;
using Repositories;

namespace Persistence.PostgreSql.Dapper;

internal sealed class QueueObservationsRepository(IDbConnectionFactory connectionFactory)
    : IQueueObservationsRepository
{
    private const string SelectColumns =
        "id, collection_run_id AS CollectionRunId, park_id AS ParkId, land_id AS LandId, " +
        "attraction_id AS AttractionId, collected_at AS CollectedAt, " +
        "observed_local_date AS ObservedLocalDate, observed_local_time AS ObservedLocalTime, " +
        "observed_local_hour AS ObservedLocalHour, observed_slot_minutes AS ObservedSlotMinutes, " +
        "observed_day_of_week AS ObservedDayOfWeek, is_open AS IsOpen, wait_minutes AS WaitMinutes, " +
        "source_last_updated AS SourceLastUpdated, created_at AS CreatedAt";

    public IEnumerable<QueueObservation> GetAll()
    {
        using var connection = connectionFactory.CreateConnection();
        return connection.Query<QueueObservation>(
            $"SELECT {SelectColumns} FROM public.queue_observations").ToList();
    }

    public QueueObservation? GetById(int id)
    {
        using var connection = connectionFactory.CreateConnection();
        return connection.QuerySingleOrDefault<QueueObservation>(
            $"SELECT {SelectColumns} FROM public.queue_observations WHERE id = @Id",
            new { Id = id });
    }

    public QueueObservation InsertOrUpdate(QueueObservation entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (entity.CreatedAt == default)
        {
            entity.CreatedAt = DateTimeOffset.UtcNow;
        }

        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            INSERT INTO public.queue_observations
                (collection_run_id, park_id, land_id, attraction_id, collected_at,
                 observed_local_date, observed_local_time, observed_local_hour,
                 observed_slot_minutes, observed_day_of_week, is_open, wait_minutes,
                 source_last_updated, created_at)
            VALUES
                (@CollectionRunId, @ParkId, @LandId, @AttractionId, @CollectedAt,
                 @ObservedLocalDate, @ObservedLocalTime, @ObservedLocalHour,
                 @ObservedSlotMinutes, @ObservedDayOfWeek, @IsOpen, @WaitMinutes,
                 @SourceLastUpdated, @CreatedAt)
            ON CONFLICT ON CONSTRAINT uq_queue_observations_attraction_date_slot
            DO UPDATE SET
              collection_run_id = EXCLUDED.collection_run_id,
              collected_at = EXCLUDED.collected_at,
              is_open = EXCLUDED.is_open,
              wait_minutes = EXCLUDED.wait_minutes,
              source_last_updated = EXCLUDED.source_last_updated
            RETURNING id, collection_run_id AS CollectionRunId, park_id AS ParkId,
                      land_id AS LandId, attraction_id AS AttractionId,
                      collected_at AS CollectedAt, observed_local_date AS ObservedLocalDate,
                      observed_local_time AS ObservedLocalTime,
                      observed_local_hour AS ObservedLocalHour,
                      observed_slot_minutes AS ObservedSlotMinutes,
                      observed_day_of_week AS ObservedDayOfWeek, is_open AS IsOpen,
                      wait_minutes AS WaitMinutes, source_last_updated AS SourceLastUpdated,
                      created_at AS CreatedAt;
            """;

        return connection.QuerySingle<QueueObservation>(sql, entity);
    }

    public bool DeleteById(int id) =>
        throw new NotSupportedException("Delete is not supported for queue observations.");
}
