using Dapper;
using Repositories;

namespace Persistence.PostgreSql.Dapper;

internal sealed class QueueCollectionRunsRepository(IDbConnectionFactory connectionFactory)
    : IQueueCollectionRunsRepository
{
    public QueueCollectionRun Start(int parkId, DateTimeOffset startedAt)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            INSERT INTO public.queue_collection_runs
                (park_id, started_at, completed_at, success, error_message)
            VALUES
                (@ParkId, @StartedAt, NULL, FALSE, NULL)
            RETURNING id, park_id AS ParkId, started_at AS StartedAt,
                      completed_at AS CompletedAt, success, error_message AS ErrorMessage;
            """;

        return connection.QuerySingle<QueueCollectionRun>(
            sql,
            new { ParkId = parkId, StartedAt = startedAt });
    }

    public void Complete(
        int id,
        DateTimeOffset completedAt,
        bool success,
        string? errorMessage = null)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            UPDATE public.queue_collection_runs
            SET completed_at = @CompletedAt,
                success = @Success,
                error_message = @ErrorMessage
            WHERE id = @Id;
            """;

        var updatedRows = connection.Execute(
            sql,
            new
            {
                Id = id,
                CompletedAt = completedAt,
                Success = success,
                ErrorMessage = errorMessage
            });

        if (updatedRows != 1)
        {
            throw new InvalidOperationException(
                $"Expected to complete collection run {id}, but updated {updatedRows} rows.");
        }
    }
}
