using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Disney.Infrastructure;

internal sealed class PostgreSqlConnectionFactory
{
    private readonly string _connectionString;

    public PostgreSqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString =
            configuration.GetConnectionString("Default") ??
            configuration.GetConnectionString("DefaultConnection") ??
            throw new InvalidOperationException(
                "Connection string 'Default' or 'DefaultConnection' is required.");
    }

    public DbConnection CreateConnection() => new NpgsqlConnection(_connectionString);
}
