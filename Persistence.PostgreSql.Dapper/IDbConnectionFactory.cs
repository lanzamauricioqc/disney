using System.Data.Common;

namespace Persistence.PostgreSql.Dapper;

internal interface IDbConnectionFactory
{
    DbConnection CreateConnection();
}
