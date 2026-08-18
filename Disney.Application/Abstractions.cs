using Disney.Domain;

namespace Disney.Application;

public interface IParkReader
{
    Task<IReadOnlyList<Park>> GetAllAsync(CancellationToken cancellationToken);
}

public interface IQueueTimesProvider
{
    Task<QueueTimesSnapshot> GetQueueTimesForParkAsync(
        int sourceParkId,
        CancellationToken cancellationToken);
}

public interface IQueueCollectionStore
{
    Task<long> StartRunAsync(
        long parkId,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken);

    Task<CollectionResult> PersistSuccessfulRunAsync(
        long runId,
        Park park,
        QueueTimesSnapshot snapshot,
        DateTimeOffset collectedAt,
        CancellationToken cancellationToken);

    Task FailRunAsync(
        long runId,
        DateTimeOffset completedAt,
        string errorMessage,
        CancellationToken cancellationToken);
}

public interface IQueueCollectionService
{
    Task<CollectionResult> CollectAsync(Park park, CancellationToken cancellationToken);
}

public interface IQueueCollectionJob
{
    Task ExecuteAsync(CancellationToken cancellationToken);
}

public interface IDatabaseMigrator
{
    Task MigrateAsync(CancellationToken cancellationToken = default);
}

public interface IDatabaseHealthCheck
{
    Task CheckAsync(CancellationToken cancellationToken = default);
}
