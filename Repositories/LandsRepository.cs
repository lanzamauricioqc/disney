using System;
using System.Data.Common;
using Dapper;
using Repositories.Interfaces;

namespace Repositories
{
    public class LandsRepository : IRepository<Land, int>
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public LandsRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public IEnumerable<Land> GetAll()
        {
            using var conn = _connectionFactory.CreateConnection();
            conn.Open();

            var sql = @"SELECT id, park_id AS ParkId, source_land_id AS SourceLandId, name, is_active AS IsActive, created_at AS CreatedAt, updated_at AS UpdatedAt FROM public.lands";
            return conn.Query<Land>(sql);
        }

        public Land GetById(int id)
        {
            using var conn = _connectionFactory.CreateConnection();
            conn.Open();

            var sql = @"SELECT id, park_id AS ParkId, source_land_id AS SourceLandId, name, is_active AS IsActive, created_at AS CreatedAt, updated_at AS UpdatedAt FROM public.lands WHERE id = @Id";
            return conn.QuerySingleOrDefault<Land>(sql, new { Id = id });
        }

        public Land InsertOrUpdate(Land entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            if (entity.CreatedAt == default) entity.CreatedAt = DateTimeOffset.UtcNow;
            entity.UpdatedAt = DateTimeOffset.UtcNow;

            using var conn = _connectionFactory.CreateConnection();
            conn.Open();

            if (entity.Id == 0)
            {
                var insertSql = @"
INSERT INTO public.lands (park_id, source_land_id, name, is_active, created_at, updated_at)
VALUES (@ParkId, @SourceLandId, @Name, @IsActive, @CreatedAt, @UpdatedAt)
RETURNING id, park_id AS ParkId, source_land_id AS SourceLandId, name, is_active AS IsActive, created_at AS CreatedAt, updated_at AS UpdatedAt;";

                return conn.QuerySingle<Land>(insertSql, entity);
            }

            var upsertSql = @"
INSERT INTO public.lands (id, park_id, source_land_id, name, is_active, created_at, updated_at)
VALUES (@Id, @ParkId, @SourceLandId, @Name, @IsActive, @CreatedAt, @UpdatedAt)
ON CONFLICT (id) DO UPDATE
  SET park_id = EXCLUDED.park_id,
      source_land_id = EXCLUDED.source_land_id,
      name = EXCLUDED.name,
      is_active = EXCLUDED.is_active,
      updated_at = EXCLUDED.updated_at
RETURNING id, park_id AS ParkId, source_land_id AS SourceLandId, name, is_active AS IsActive, created_at AS CreatedAt, updated_at AS UpdatedAt;";

            return conn.QuerySingle<Land>(upsertSql, entity);
        }

        public bool DeleteById(int id)
        {
            using var conn = _connectionFactory.CreateConnection();
            conn.Open();

            var sql = "DELETE FROM public.lands WHERE id = @Id";
            var rows = conn.Execute(sql, new { Id = id });
            return rows > 0;
        }
    }
}
