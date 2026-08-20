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
            SELECT park.id, park.source_park_id AS SourceParkId, park.name, park.timezone,
                   park.is_active AS IsActive,
                   park.collection_enabled AS CollectionEnabled,
                   park.collection_interval_minutes AS CollectionIntervalMinutes,
                   latest_run.started_at AS LastCollectionStartedAt,
                   park.created_at AS CreatedAt, park.updated_at AS UpdatedAt
            FROM public.parks park
            LEFT JOIN LATERAL (
                SELECT run.started_at
                FROM public.queue_collection_runs run
                WHERE run.park_id = park.id
                ORDER BY run.started_at DESC
                LIMIT 1
            ) latest_run ON TRUE
            WHERE park.is_active
            ORDER BY id;
            """,
            cancellationToken: cancellationToken));
        return parks.AsList();
    }
}
