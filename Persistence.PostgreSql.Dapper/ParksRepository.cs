using Dapper;
using Repositories;

namespace Persistence.PostgreSql.Dapper;

internal sealed class ParksRepository(IDbConnectionFactory connectionFactory) : IParksRepository
{
    private const string SelectColumns =
        "id, source_park_id AS SourceParkId, name, timezone, created_at AS CreatedAt, updated_at AS UpdatedAt";

    public IReadOnlyList<Park> GetAll()
    {
        using var connection = connectionFactory.CreateConnection();
        return connection.Query<Park>($"SELECT {SelectColumns} FROM public.parks").ToList();
    }
}
