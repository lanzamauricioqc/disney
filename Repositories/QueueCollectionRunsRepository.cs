using System;
using System.Data.Common;
using Dapper;
using Repositories.Interfaces;

namespace Repositories
{
    public class QueueCollectionRunsRepository : IRepository<QueueCollectionRun, int>
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public QueueCollectionRunsRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public IEnumerable<QueueCollectionRun> GetAll()
        {
            using var conn = _connectionFactory.CreateConnection();
            conn.Open();

            var sql = @"SELECT id, park_id AS ParkId, started_at AS StartedAt, completed_at AS CompletedAt, success, error_message AS ErrorMessage FROM public.queue_collection_runs";
            return conn.Query<QueueCollectionRun>(sql);
        }

        public QueueCollectionRun GetById(int id)
        {
            using var conn = _connectionFactory.CreateConnection();
            conn.Open();

            var sql = @"SELECT id, park_id AS ParkId, started_at AS StartedAt, completed_at AS CompletedAt, success, error_message AS ErrorMessage FROM public.queue_collection_runs WHERE id = @Id";
            return conn.QuerySingleOrDefault<QueueCollectionRun>(sql, new { Id = id });
        }

        public QueueCollectionRun InsertOrUpdate(QueueCollectionRun entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            // Simplified behavior: only insert new collection runs. Let DB generate id.
            using var conn = _connectionFactory.CreateConnection();
            conn.Open();

            var insertSql = @"
INSERT INTO public.queue_collection_runs (park_id, started_at, completed_at, success, error_message)
VALUES (@ParkId, @StartedAt, @CompletedAt, @Success, @ErrorMessage)
RETURNING id, park_id AS ParkId, started_at AS StartedAt, completed_at AS CompletedAt, success, error_message AS ErrorMessage;";

            return conn.QuerySingle<QueueCollectionRun>(insertSql, entity);
        }

        public bool DeleteById(int id)
        {
            // Deletes are not supported for queue tables in this system.
            throw new NotImplementedException("Delete is not supported for queue_collection_runs");
        }
    }
}