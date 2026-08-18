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
        var job = new StubJob(cancellation);
        var services = new ServiceCollection();
        services.AddScoped<IQueueCollectionJob>(_ => job);
        using var provider = services.BuildServiceProvider();
        var worker = new QueueCollectionWorker(
            NullLogger<QueueCollectionWorker>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new QueueCollectionOptions { Interval = TimeSpan.Zero }));

        await worker.StartAsync(cancellation.Token);
        await worker.ExecuteTask!;

        Assert.Equal(2, job.Executions);
    }

    private sealed class StubJob(CancellationTokenSource cancellation) : IQueueCollectionJob
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
