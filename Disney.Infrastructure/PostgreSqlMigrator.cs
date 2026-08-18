using System.Reflection;
using Dapper;
using Disney.Application;

namespace Disney.Infrastructure;

internal sealed class PostgreSqlMigrator(
    PostgreSqlConnectionFactory connectionFactory) : IDatabaseMigrator
{
    private const string ResourcePrefix = "Disney.Infrastructure.Migrations.";

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "SELECT pg_advisory_lock(764921357);",
            cancellationToken: cancellationToken));

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                CREATE TABLE IF NOT EXISTS public.schema_migrations (
                    version text PRIMARY KEY,
                    applied_at timestamptz NOT NULL DEFAULT now()
                );
                """,
                cancellationToken: cancellationToken));

            var assembly = typeof(PostgreSqlMigrator).Assembly;
            var resources = assembly.GetManifestResourceNames()
                .Where(x => x.StartsWith(ResourcePrefix, StringComparison.Ordinal))
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            foreach (var resource in resources)
            {
                var version = resource[ResourcePrefix.Length..^4];
                var applied = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                    "SELECT EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = @Version);",
                    new { Version = version },
                    cancellationToken: cancellationToken));
                if (applied)
                {
                    continue;
                }

                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                try
                {
                    await using var stream = assembly.GetManifestResourceStream(resource)
                        ?? throw new InvalidOperationException(
                            $"Migration resource '{resource}' was not found.");
                    using var reader = new StreamReader(stream);
                    var sql = await reader.ReadToEndAsync(cancellationToken);
                    await connection.ExecuteAsync(new CommandDefinition(
                        sql,
                        transaction: transaction,
                        cancellationToken: cancellationToken));
                    await connection.ExecuteAsync(new CommandDefinition(
                        "INSERT INTO public.schema_migrations (version) VALUES (@Version);",
                        new { Version = version },
                        transaction,
                        cancellationToken: cancellationToken));
                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    throw;
                }
            }
        }
        finally
        {
            await connection.ExecuteAsync(
                new CommandDefinition("SELECT pg_advisory_unlock(764921357);"));
        }
    }
}
