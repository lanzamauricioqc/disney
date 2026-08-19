using Disney.Application;
using Disney.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Disney.Tests;

public sealed class WorkerTests
{
    [Fact]
    public async Task Worker_StopsAfterCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var collectionJob = new StubQueueCollectionJob(cancellation);
        var services = new ServiceCollection();
        services.AddScoped<IQueueCollectionJob>(_ => collectionJob);
        using var serviceProvider = services.BuildServiceProvider();
        var collectionWorker = new QueueCollectionWorker(
            NullLogger<QueueCollectionWorker>.Instance,
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new QueueCollectionOptions { Interval = TimeSpan.Zero }));

        await collectionWorker.StartAsync(cancellation.Token);
        await collectionWorker.ExecuteTask!;

        Assert.Equal(2, collectionJob.Executions);
    }

    private sealed class StubQueueCollectionJob(CancellationTokenSource cancellation)
        : IQueueCollectionJob
    {
        public int Executions { get; private set; }

        public Task ExecuteAsync(CancellationToken cancellationToken)
        {
            Executions++;
            if (Executions == 2)
            {
                cancellation.Cancel();
                return Task.FromCanceled(cancellation.Token);
            }

            return Task.CompletedTask;
        }
    }
}
