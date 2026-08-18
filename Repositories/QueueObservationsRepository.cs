using System;
using System.Data.Common;
using Dapper;
using Repositories.Interfaces;

namespace Repositories
{
    public class QueueObservationsRepository : IRepository<QueueObservation, int>
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public QueueObservationsRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public IEnumerable<QueueObservation> GetAll()
        {
            using var conn = _connectionFactory.CreateConnection();
            conn.Open();

            var sql = @"SELECT id, collection_run_id AS CollectionRunId, park_id AS ParkId, land_id AS LandId, attraction_id AS AttractionId, collected_at AS CollectedAt, observed_local_date AS ObservedLocalDate, observed_local_time AS ObservedLocalTime, observed_local_hour AS ObservedLocalHour, observed_slot_minutes AS ObservedSlotMinutes, observed_day_of_week AS ObservedDayOfWeek, is_open AS IsOpen, wait_minutes AS WaitMinutes, source_last_updated AS SourceLastUpdated, created_at AS CreatedAt FROM public.queue_observations";
            return conn.Query<QueueObservation>(sql);
        }

        public QueueObservation GetById(int id)
        {
            using var conn = _connectionFactory.CreateConnection();
            conn.Open();

            var sql = @"SELECT id, collection_run_id AS CollectionRunId, park_id AS ParkId, land_id AS LandId, attraction_id AS AttractionId, collected_at AS CollectedAt, observed_local_date AS ObservedLocalDate, observed_local_time AS ObservedLocalTime, observed_local_hour AS ObservedLocalHour, observed_slot_minutes AS ObservedSlotMinutes, observed_day_of_week AS ObservedDayOfWeek, is_open AS IsOpen, wait_minutes AS WaitMinutes, source_last_updated AS SourceLastUpdated, created_at AS CreatedAt FROM public.queue_observations WHERE id = @Id";
            return conn.QuerySingleOrDefault<QueueObservation>(sql, new { Id = id });
        }

        public QueueObservation InsertOrUpdate(QueueObservation entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            if (entity.CreatedAt == default) entity.CreatedAt = DateTimeOffset.UtcNow;

            using var conn = _connectionFactory.CreateConnection();
            conn.Open();

            // Use an upsert to avoid duplicate-key errors when the same attraction/slot
            // is recorded more than once. Update only the mutable fields on conflict
            // and return the resulting row.
            var insertSql = @"
INSERT INTO public.queue_observations (collection_run_id, park_id, land_id, attraction_id, collected_at, observed_local_date, observed_local_time, observed_local_hour, observed_slot_minutes, observed_day_of_week, is_open, wait_minutes, source_last_updated, created_at)
VALUES (@CollectionRunId, @ParkId, @LandId, @AttractionId, @CollectedAt, @ObservedLocalDate, @ObservedLocalTime, @ObservedLocalHour, @ObservedSlotMinutes, @ObservedDayOfWeek, @IsOpen, @WaitMinutes, @SourceLastUpdated, @CreatedAt)
ON CONFLICT ON CONSTRAINT uq_queue_observations_attraction_date_slot
DO UPDATE SET
  collection_run_id = EXCLUDED.collection_run_id,
  collected_at = EXCLUDED.collected_at,
  is_open = EXCLUDED.is_open,
  wait_minutes = EXCLUDED.wait_minutes,
  source_last_updated = EXCLUDED.source_last_updated
RETURNING id, collection_run_id AS CollectionRunId, park_id AS ParkId, land_id AS LandId, attraction_id AS AttractionId, collected_at AS CollectedAt, observed_local_date AS ObservedLocalDate, observed_local_time AS ObservedLocalTime, observed_local_hour AS ObservedLocalHour, observed_slot_minutes AS ObservedSlotMinutes, observed_day_of_week AS ObservedDayOfWeek, is_open AS IsOpen, wait_minutes AS WaitMinutes, source_last_updated AS SourceLastUpdated, created_at AS CreatedAt;";

            return conn.QuerySingle<QueueObservation>(insertSql, entity);
        }

        public bool DeleteById(int id)
        {
            // Deletes are not supported for queue tables in this system.
            throw new NotImplementedException("Delete is not supported for queue_observations");
        }
    }
}
