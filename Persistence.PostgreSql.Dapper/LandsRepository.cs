using Dapper;
using Repositories;

namespace Persistence.PostgreSql.Dapper;

internal sealed class LandsRepository(IDbConnectionFactory connectionFactory) : ILandsRepository
{
    private const string SelectColumns =
        "id, park_id AS ParkId, source_land_id AS SourceLandId, name, is_active AS IsActive, " +
        "created_at AS CreatedAt, updated_at AS UpdatedAt";

    public IEnumerable<Land> GetAll()
    {
        using var connection = connectionFactory.CreateConnection();
        return connection.Query<Land>($"SELECT {SelectColumns} FROM public.lands").ToList();
    }

    public Land? GetById(int id)
    {
        using var connection = connectionFactory.CreateConnection();
        return connection.QuerySingleOrDefault<Land>(
            $"SELECT {SelectColumns} FROM public.lands WHERE id = @Id",
            new { Id = id });
    }

    public Land InsertOrUpdate(Land entity)
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
                INSERT INTO public.lands (park_id, source_land_id, name, is_active, created_at, updated_at)
                VALUES (@ParkId, @SourceLandId, @Name, @IsActive, @CreatedAt, @UpdatedAt)
                RETURNING id, park_id AS ParkId, source_land_id AS SourceLandId, name,
                          is_active AS IsActive, created_at AS CreatedAt, updated_at AS UpdatedAt;
                """;

            return connection.QuerySingle<Land>(insertSql, entity);
        }

        const string upsertSql = """
            INSERT INTO public.lands (id, park_id, source_land_id, name, is_active, created_at, updated_at)
            VALUES (@Id, @ParkId, @SourceLandId, @Name, @IsActive, @CreatedAt, @UpdatedAt)
            ON CONFLICT (id) DO UPDATE
              SET park_id = EXCLUDED.park_id,
                  source_land_id = EXCLUDED.source_land_id,
                  name = EXCLUDED.name,
                  is_active = EXCLUDED.is_active,
                  updated_at = EXCLUDED.updated_at
            RETURNING id, park_id AS ParkId, source_land_id AS SourceLandId, name,
                      is_active AS IsActive, created_at AS CreatedAt, updated_at AS UpdatedAt;
            """;

        return connection.QuerySingle<Land>(upsertSql, entity);
    }

    public bool DeleteById(int id)
    {
        using var connection = connectionFactory.CreateConnection();
        return connection.Execute("DELETE FROM public.lands WHERE id = @Id", new { Id = id }) > 0;
    }
}
