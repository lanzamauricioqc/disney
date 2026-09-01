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

    [Fact]
    public async Task DatabaseStartupInitializer_CompletesWhenDatabaseIsAvailable()
    {
        var migrator = new StubDatabaseMigrator();
        var healthCheck = new StubDatabaseHealthCheck();
        var initializer = CreateDatabaseStartupInitializer(
            migrator,
            healthCheck,
            maxAttempts: 1);

        await initializer.InitializeAsync();

        Assert.Equal(1, migrator.Attempts);
        Assert.Equal(1, healthCheck.Attempts);
    }

    [Fact]
    public async Task DatabaseStartupInitializer_RetriesTransientFailures()
    {
        var migrator = new StubDatabaseMigrator(failuresBeforeSuccess: 1);
        var healthCheck = new StubDatabaseHealthCheck(failuresBeforeSuccess: 1);
        var initializer = CreateDatabaseStartupInitializer(
            migrator,
            healthCheck,
            maxAttempts: 3,
            initialDelay: TimeSpan.FromMilliseconds(1),
            maxDelay: TimeSpan.FromMilliseconds(4));

        await initializer.InitializeAsync();

        Assert.Equal(3, migrator.Attempts);
        Assert.Equal(2, healthCheck.Attempts);
    }

    [Fact]
    public async Task DatabaseStartupInitializer_StopsAfterMaximumAttempts()
    {
        var migrator = new StubDatabaseMigrator(failuresBeforeSuccess: int.MaxValue);
        var healthCheck = new StubDatabaseHealthCheck();
        var initializer = CreateDatabaseStartupInitializer(
            migrator,
            healthCheck,
            maxAttempts: 2,
            initialDelay: TimeSpan.FromMilliseconds(1),
            maxDelay: TimeSpan.FromMilliseconds(1));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => initializer.InitializeAsync());

        Assert.Equal(2, migrator.Attempts);
        Assert.Equal(0, healthCheck.Attempts);
    }

    [Fact]
    public async Task DatabaseStartupInitializer_DoesNotRetryCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var migrator = new StubDatabaseMigrator(cancel: true);
        var healthCheck = new StubDatabaseHealthCheck();
        var initializer = CreateDatabaseStartupInitializer(
            migrator,
            healthCheck,
            maxAttempts: 2);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => initializer.InitializeAsync(cancellation.Token));

        Assert.Equal(1, migrator.Attempts);
        Assert.Equal(0, healthCheck.Attempts);
    }

    private static DatabaseStartupInitializer CreateDatabaseStartupInitializer(
        IDatabaseMigrator migrator,
        IDatabaseHealthCheck healthCheck,
        int maxAttempts,
        TimeSpan? initialDelay = null,
        TimeSpan? maxDelay = null) =>
        new(
            migrator,
            healthCheck,
            Options.Create(new DatabaseStartupOptions
            {
                MaxAttempts = maxAttempts,
                InitialDelay = initialDelay ?? TimeSpan.FromMilliseconds(1),
                MaxDelay = maxDelay ?? TimeSpan.FromMilliseconds(1)
            }),
            NullLogger<DatabaseStartupInitializer>.Instance);

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

    private sealed class StubDatabaseMigrator(
        int failuresBeforeSuccess = 0,
        bool cancel = false) : IDatabaseMigrator
    {
        public int Attempts { get; private set; }

        public Task MigrateAsync(CancellationToken cancellationToken = default)
        {
            Attempts++;

            if (cancel)
            {
                return Task.FromCanceled(cancellationToken);
            }

            if (Attempts <= failuresBeforeSuccess)
            {
                throw new InvalidOperationException("Database is unavailable.");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class StubDatabaseHealthCheck(int failuresBeforeSuccess = 0)
        : IDatabaseHealthCheck
    {
        public int Attempts { get; private set; }

        public Task CheckAsync(CancellationToken cancellationToken = default)
        {
            Attempts++;

            if (Attempts <= failuresBeforeSuccess)
            {
                throw new InvalidOperationException("Database is unavailable.");
            }

            return Task.CompletedTask;
        }
    }
}
