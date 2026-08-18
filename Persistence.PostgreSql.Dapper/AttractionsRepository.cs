using Dapper;
using Repositories;

namespace Persistence.PostgreSql.Dapper;

internal sealed class AttractionsRepository(IDbConnectionFactory connectionFactory) : IAttractionsRepository
{
    private const string SelectColumns =
        "id, park_id AS ParkId, current_land_id AS CurrentLandId, source_ride_id AS SourceRideId, " +
        "name, is_active AS IsActive, created_at AS CreatedAt, updated_at AS UpdatedAt";

    public IEnumerable<Attraction> GetAll()
    {
        using var connection = connectionFactory.CreateConnection();
        return connection.Query<Attraction>($"SELECT {SelectColumns} FROM public.attractions").ToList();
    }

    public Attraction? GetById(int id)
    {
        using var connection = connectionFactory.CreateConnection();
        return connection.QuerySingleOrDefault<Attraction>(
            $"SELECT {SelectColumns} FROM public.attractions WHERE id = @Id",
            new { Id = id });
    }

    public Attraction InsertOrUpdate(Attraction entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (entity.CreatedAt == default)
        {
            entity.CreatedAt = DateTimeOffset.UtcNow;
        }

        entity.UpdatedAt = DateTimeOffset.UtcNow;

        using var connection = connectionFactory.CreateConnection();

        if (entity.Id == 0)
        {
            const string insertSql = """
                INSERT INTO public.attractions
                    (park_id, current_land_id, source_ride_id, name, is_active, created_at, updated_at)
                VALUES
                    (@ParkId, @CurrentLandId, @SourceRideId, @Name, @IsActive, @CreatedAt, @UpdatedAt)
                RETURNING id, park_id AS ParkId, current_land_id AS CurrentLandId,
                          source_ride_id AS SourceRideId, name, is_active AS IsActive,
                          created_at AS CreatedAt, updated_at AS UpdatedAt;
                """;

            return connection.QuerySingle<Attraction>(insertSql, entity);
        }

        const string upsertSql = """
            INSERT INTO public.attractions
                (id, park_id, current_land_id, source_ride_id, name, is_active, created_at, updated_at)
            VALUES
                (@Id, @ParkId, @CurrentLandId, @SourceRideId, @Name, @IsActive, @CreatedAt, @UpdatedAt)
            ON CONFLICT (id) DO UPDATE
              SET park_id = EXCLUDED.park_id,
                  current_land_id = EXCLUDED.current_land_id,
                  source_ride_id = EXCLUDED.source_ride_id,
                  name = EXCLUDED.name,
                  is_active = EXCLUDED.is_active,
                  updated_at = EXCLUDED.updated_at
            RETURNING id, park_id AS ParkId, current_land_id AS CurrentLandId,
                      source_ride_id AS SourceRideId, name, is_active AS IsActive,
                      created_at AS CreatedAt, updated_at AS UpdatedAt;
            """;

        return connection.QuerySingle<Attraction>(upsertSql, entity);
    }

    public bool DeleteById(int id)
    {
        using var connection = connectionFactory.CreateConnection();
        return connection.Execute("DELETE FROM public.attractions WHERE id = @Id", new { Id = id }) > 0;
    }
}
