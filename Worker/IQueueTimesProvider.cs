namespace WorkerModels;

public interface IQueueTimesProvider
{
    Task<WaitingTimeModel?> GetQueueTimesForParkAsync(
        int sourceParkId,
        CancellationToken cancellationToken);
}
