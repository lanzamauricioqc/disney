using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Persistence.PostgreSql.Dapper;

internal sealed class NpgsqlConnectionFactory(IConfiguration configuration) : IDbConnectionFactory
{
    private readonly string _connectionString =
        configuration.GetConnectionString("Default")
        ?? configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            "Connection string 'Default' (or 'DefaultConnection') is not configured. " +
            "Add it to appsettings.json or set 'ConnectionStrings__Default'.");

    public DbConnection CreateConnection() => new NpgsqlConnection(_connectionString);
}
