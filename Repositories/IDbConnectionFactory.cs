using System.Data.Common;

namespace Repositories
{
    public interface IDbConnectionFactory
    {
        DbConnection CreateConnection();
    }
}
