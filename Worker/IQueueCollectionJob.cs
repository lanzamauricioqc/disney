namespace WorkerModels;

internal interface IQueueCollectionJob
{
    Task ExecuteAsync(CancellationToken cancellationToken);
}
