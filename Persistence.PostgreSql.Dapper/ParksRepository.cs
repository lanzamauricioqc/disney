using Dapper;
using Repositories;

namespace Persistence.PostgreSql.Dapper;

internal sealed class ParksRepository(IDbConnectionFactory connectionFactory) : IParksRepository
{
    private const string SelectColumns =
        "id, source_park_id AS SourceParkId, name, timezone, created_at AS CreatedAt, updated_at AS UpdatedAt";

    public IEnumerable<Park> GetAll()
    {
        using var connection = connectionFactory.CreateConnection();
        return connection.Query<Park>($"SELECT {SelectColumns} FROM public.parks").ToList();
    }

    public Park? GetById(int id)
    {
        using var connection = connectionFactory.CreateConnection();
        return connection.QuerySingleOrDefault<Park>(
            $"SELECT {SelectColumns} FROM public.parks WHERE id = @Id",
            new { Id = id });
    }

    public Park InsertOrUpdate(Park entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (entity.CreatedAt == default)
        {
            entity.CreatedAt = DateTimeOffset.UtcNow;
        }

        entity.UpdatedAt = DateTimeOffset.UtcNow;

        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            INSERT INTO public.parks (id, source_park_id, name, timezone, created_at, updated_at)
            VALUES (@Id, @SourceParkId, @Name, @Timezone, @CreatedAt, @UpdatedAt)
            ON CONFLICT (id) DO UPDATE
              SET source_park_id = EXCLUDED.source_park_id,
                  name = EXCLUDED.name,
                  timezone = EXCLUDED.timezone,
                  updated_at = EXCLUDED.updated_at
            RETURNING id, source_park_id AS SourceParkId, name, timezone,
                      created_at AS CreatedAt, updated_at AS UpdatedAt;
            """;

        return connection.QuerySingle<Park>(sql, entity);
    }

    public bool DeleteById(int id)
    {
        using var connection = connectionFactory.CreateConnection();
        return connection.Execute("DELETE FROM public.parks WHERE id = @Id", new { Id = id }) > 0;
    }
}
