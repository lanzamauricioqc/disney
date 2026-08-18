using Repositories;

namespace Persistence.PostgreSql.Dapper;

internal sealed class DatabaseHealthCheck(IDbConnectionFactory connectionFactory) : IDatabaseHealthCheck
{
    public async Task CheckAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        await command.ExecuteScalarAsync(cancellationToken);
    }
}
