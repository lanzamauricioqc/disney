using Dapper;
using Disney.Application;

namespace Disney.Infrastructure;

internal sealed class PostgreSqlDatabaseHealthCheck(
    PostgreSqlConnectionFactory connectionFactory) : IDatabaseHealthCheck
{
    public async Task CheckAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteScalarAsync<int>(
            new CommandDefinition("SELECT 1;", cancellationToken: cancellationToken));
    }
}

internal sealed class PostgreSqlParkReader(
    PostgreSqlConnectionFactory connectionFactory) : IParkReader
{
    public async Task<IReadOnlyList<Domain.Park>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var parks = await connection.QueryAsync<Domain.Park>(new CommandDefinition(
            """
            SELECT id, source_park_id AS SourceParkId, name, timezone,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM public.parks
            ORDER BY id;
            """,
            cancellationToken: cancellationToken));
        return parks.AsList();
    }
}
