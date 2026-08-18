using Dapper;
using Repositories;

namespace Persistence.PostgreSql.Dapper;

internal sealed class QueueCollectionRunsRepository(IDbConnectionFactory connectionFactory)
    : IQueueCollectionRunsRepository
{
    private const string SelectColumns =
        "id, park_id AS ParkId, started_at AS StartedAt, completed_at AS CompletedAt, " +
        "success, error_message AS ErrorMessage";

    public IEnumerable<QueueCollectionRun> GetAll()
    {
        using var connection = connectionFactory.CreateConnection();
        return connection.Query<QueueCollectionRun>(
            $"SELECT {SelectColumns} FROM public.queue_collection_runs").ToList();
    }

    public QueueCollectionRun? GetById(int id)
    {
        using var connection = connectionFactory.CreateConnection();
        return connection.QuerySingleOrDefault<QueueCollectionRun>(
            $"SELECT {SelectColumns} FROM public.queue_collection_runs WHERE id = @Id",
            new { Id = id });
    }

    public QueueCollectionRun InsertOrUpdate(QueueCollectionRun entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            INSERT INTO public.queue_collection_runs
                (park_id, started_at, completed_at, success, error_message)
            VALUES
                (@ParkId, @StartedAt, @CompletedAt, @Success, @ErrorMessage)
            RETURNING id, park_id AS ParkId, started_at AS StartedAt,
                      completed_at AS CompletedAt, success, error_message AS ErrorMessage;
            """;

        return connection.QuerySingle<QueueCollectionRun>(sql, entity);
    }

    public bool DeleteById(int id) =>
        throw new NotSupportedException("Delete is not supported for queue collection runs.");
}
