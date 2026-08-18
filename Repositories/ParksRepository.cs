using System.Data.Common;
using Dapper;
using Repositories.Interfaces;

namespace Repositories
{
    public class ParksRepository : IRepository<Park, Int32>
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public ParksRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public IEnumerable<Park> GetAll()
        {
            using var conn = _connectionFactory.CreateConnection();
            conn.Open();

            var sql = @"SELECT id, source_park_id AS SourceParkId, name, timezone, created_at AS CreatedAt, updated_at AS UpdatedAt FROM public.parks";
            return conn.Query<Park>(sql);
        }

        public Park GetById(int id)
        {
            using var conn = _connectionFactory.CreateConnection();
            conn.Open();

            var sql = @"SELECT id, source_park_id AS SourceParkId, name, timezone, created_at AS CreatedAt, updated_at AS UpdatedAt FROM public.parks WHERE id = @Id";
            return conn.QuerySingleOrDefault<Park>(sql, new { Id = id });
        }

        public Park InsertOrUpdate(Park entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            if (entity.CreatedAt == default)
            {
                entity.CreatedAt = DateTimeOffset.UtcNow;
            }

            entity.UpdatedAt = DateTimeOffset.UtcNow;

            using var conn = _connectionFactory.CreateConnection();
            conn.Open();

            var sql = @"
INSERT INTO public.parks (id, source_park_id, name, timezone, created_at, updated_at)
VALUES (@Id, @SourceParkId, @Name, @Timezone, @CreatedAt, @UpdatedAt)
ON CONFLICT (id) DO UPDATE
  SET source_park_id = EXCLUDED.source_park_id,
      name = EXCLUDED.name,
      timezone = EXCLUDED.timezone,
      updated_at = EXCLUDED.updated_at
RETURNING id, source_park_id AS SourceParkId, name, timezone, created_at AS CreatedAt, updated_at AS UpdatedAt;";

            return conn.QuerySingle<Park>(sql, entity);
        }

        public bool DeleteById(int id)
        {
            using var conn = _connectionFactory.CreateConnection();
            conn.Open();

            var sql = "DELETE FROM public.parks WHERE id = @Id";
            var rows = conn.Execute(sql, new { Id = id });
            return rows > 0;
        }
    }
}