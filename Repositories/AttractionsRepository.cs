using System;
using System.Data.Common;
using Dapper;
using Repositories.Interfaces;

namespace Repositories
{
    public class AttractionsRepository : IRepository<Attraction, int>
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public AttractionsRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public IEnumerable<Attraction> GetAll()
        {
            using var conn = _connectionFactory.CreateConnection();
            conn.Open();

            var sql = @"SELECT id, park_id AS ParkId, current_land_id AS CurrentLandId, source_ride_id AS SourceRideId, name, is_active AS IsActive, created_at AS CreatedAt, updated_at AS UpdatedAt FROM public.attractions";
            return conn.Query<Attraction>(sql);
        }

        public Attraction GetById(int id)
        {
            using var conn = _connectionFactory.CreateConnection();
            conn.Open();

            var sql = @"SELECT id, park_id AS ParkId, current_land_id AS CurrentLandId, source_ride_id AS SourceRideId, name, is_active AS IsActive, created_at AS CreatedAt, updated_at AS UpdatedAt FROM public.attractions WHERE id = @Id";
            return conn.QuerySingleOrDefault<Attraction>(sql, new { Id = id });
        }

        public Attraction InsertOrUpdate(Attraction entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            if (entity.CreatedAt == default) entity.CreatedAt = DateTimeOffset.UtcNow;
            entity.UpdatedAt = DateTimeOffset.UtcNow;

            using var conn = _connectionFactory.CreateConnection();
            conn.Open();

            // If Id == 0 let the database generate it by inserting without the id column
            if (entity.Id == 0)
            {
                var insertSql = @"
INSERT INTO public.attractions (park_id, current_land_id, source_ride_id, name, is_active, created_at, updated_at)
VALUES (@ParkId, @CurrentLandId, @SourceRideId, @Name, @IsActive, @CreatedAt, @UpdatedAt)
RETURNING id, park_id AS ParkId, current_land_id AS CurrentLandId, source_ride_id AS SourceRideId, name, is_active AS IsActive, created_at AS CreatedAt, updated_at AS UpdatedAt;";

                return conn.QuerySingle<Attraction>(insertSql, entity);
            }

            var upsertSql = @"
INSERT INTO public.attractions (id, park_id, current_land_id, source_ride_id, name, is_active, created_at, updated_at)
VALUES (@Id, @ParkId, @CurrentLandId, @SourceRideId, @Name, @IsActive, @CreatedAt, @UpdatedAt)
ON CONFLICT (id) DO UPDATE
  SET park_id = EXCLUDED.park_id,
      current_land_id = EXCLUDED.current_land_id,
      source_ride_id = EXCLUDED.source_ride_id,
      name = EXCLUDED.name,
      is_active = EXCLUDED.is_active,
      updated_at = EXCLUDED.updated_at
RETURNING id, park_id AS ParkId, current_land_id AS CurrentLandId, source_ride_id AS SourceRideId, name, is_active AS IsActive, created_at AS CreatedAt, updated_at AS UpdatedAt;";

            return conn.QuerySingle<Attraction>(upsertSql, entity);
        }

        public bool DeleteById(int id)
        {
            using var conn = _connectionFactory.CreateConnection();
            conn.Open();

            var sql = "DELETE FROM public.attractions WHERE id = @Id";
            var rows = conn.Execute(sql, new { Id = id });
            return rows > 0;
        }
    }
}
