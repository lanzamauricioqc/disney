using Repositories;

namespace WorkerModels;

internal interface IQueueTimesCollector
{
    Task CollectAsync(Park park, CancellationToken cancellationToken);
}
