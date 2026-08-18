using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Repositories
{
    public class NpgsqlConnectionFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;

        public NpgsqlConnectionFactory(IConfiguration configuration)
        {
            // Try common keys for backward compatibility
            _connectionString = configuration.GetConnectionString("Default")
                                ?? configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                throw new InvalidOperationException("Connection string 'Default' (or 'DefaultConnection') is not configured. Add it to appsettings.json or set the environment variable 'ConnectionStrings__Default'.");
            }
        }

        public DbConnection CreateConnection()
        {
            return new NpgsqlConnection(_connectionString);
        }
    }
}
