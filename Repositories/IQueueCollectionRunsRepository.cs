namespace Repositories;

public interface IQueueCollectionRunsRepository
{
    QueueCollectionRun Start(int parkId, DateTimeOffset startedAt);

    void Complete(
        int id,
        DateTimeOffset completedAt,
        bool success,
        string? errorMessage = null);
}
