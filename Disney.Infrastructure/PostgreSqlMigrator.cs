using System.Reflection;
using System.Data.Common;
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
        await AcquireMigrationLockAsync(connection, cancellationToken);

        try
        {
            await EnsureMigrationHistoryTableAsync(connection, cancellationToken);
            var assembly = typeof(PostgreSqlMigrator).Assembly;

            foreach (var migrationResourceName in GetMigrationResourceNames(assembly))
            {
                var version = GetVersion(migrationResourceName);
                if (await IsAppliedAsync(connection, version, cancellationToken))
                {
                    continue;
                }

                await ApplyMigrationAsync(
                    connection,
                    assembly,
                    migrationResourceName,
                    version,
                    cancellationToken);
            }
        }
        finally
        {
            await ReleaseMigrationLockAsync(connection);
        }
    }

    private static Task AcquireMigrationLockAsync(
        DbConnection connection,
        CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(
            "SELECT pg_advisory_lock(764921357);",
            cancellationToken: cancellationToken));

    private static Task ReleaseMigrationLockAsync(DbConnection connection) =>
        connection.ExecuteAsync(
            new CommandDefinition("SELECT pg_advisory_unlock(764921357);"));

    private static Task EnsureMigrationHistoryTableAsync(
        DbConnection connection,
        CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(
            """
            CREATE TABLE IF NOT EXISTS public.schema_migrations (
                version text PRIMARY KEY,
                applied_at timestamptz NOT NULL DEFAULT now()
            );
            """,
            cancellationToken: cancellationToken));

    private static IReadOnlyList<string> GetMigrationResourceNames(Assembly assembly) =>
        assembly.GetManifestResourceNames()
            .Where(resourceName =>
                resourceName.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .OrderBy(resourceName => resourceName, StringComparer.Ordinal)
            .ToList();

    private static string GetVersion(string migrationResourceName) =>
        migrationResourceName[ResourcePrefix.Length..^4];

    private static Task<bool> IsAppliedAsync(
        DbConnection connection,
        string version,
        CancellationToken cancellationToken) =>
        connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = @Version);",
            new { Version = version },
            cancellationToken: cancellationToken));

    private static async Task ApplyMigrationAsync(
        DbConnection connection,
        Assembly assembly,
        string migrationResourceName,
        string version,
        CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var migrationSql = await ReadMigrationSqlAsync(
                assembly,
                migrationResourceName,
                cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(
                migrationSql,
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

    private static async Task<string> ReadMigrationSqlAsync(
        Assembly assembly,
        string migrationResourceName,
        CancellationToken cancellationToken)
    {
        await using var migrationStream = assembly.GetManifestResourceStream(migrationResourceName)
            ?? throw new InvalidOperationException(
                $"Migration resource '{migrationResourceName}' was not found.");
        using var migrationReader = new StreamReader(migrationStream);
        return await migrationReader.ReadToEndAsync(cancellationToken);
    }
}
