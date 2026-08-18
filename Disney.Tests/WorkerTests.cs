using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Repositories;
using WorkerModels;

namespace Disney.Tests;

public sealed class WorkerTests
{
    [Fact]
    public async Task QueueTimesClient_DeserializesSuccessfulResponse()
    {
        var logger = new TestLogger<QueueTimesClient>();
        using var httpClient = new HttpClient(new StubHttpMessageHandler(
            HttpStatusCode.OK,
            """
            {
              "lands": [{
                "id": 10,
                "name": "Tomorrowland",
                "rides": [{
                  "id": 20,
                  "name": "Space Mountain",
                  "is_open": true,
                  "wait_time": 35,
                  "last_updated": "2026-08-18T18:00:00Z"
                }]
              }],
              "rides": []
            }
            """))
        {
            BaseAddress = new Uri("https://queue-times.test")
        };
        var client = new QueueTimesClient(httpClient, logger);

        var result = await client.GetQueueTimesForParkAsync(
            6,
            CancellationToken.None);

        Assert.Equal("Space Mountain", Assert.Single(Assert.Single(result.Lands).Rides).Name);
        Assert.Contains(logger.Entries, entry =>
            entry.EventId == LogEvents.QueueTimesRequestStarted &&
            AssertProperty(entry, "SourceParkId", 6));
        Assert.Contains(logger.Entries, entry =>
            entry.EventId == LogEvents.QueueTimesRequestCompleted &&
            AssertProperty(entry, "StatusCode", 200));
    }

    [Fact]
    public async Task QueueTimesClient_RejectsFailuresAndEmptyPayloads()
    {
        var logger = new TestLogger<QueueTimesClient>();
        using var failedClient = new HttpClient(
            new StubHttpMessageHandler(HttpStatusCode.InternalServerError, "{}"))
        {
            BaseAddress = new Uri("https://queue-times.test")
        };
        using var emptyClient = new HttpClient(
            new StubHttpMessageHandler(HttpStatusCode.OK, "null"))
        {
            BaseAddress = new Uri("https://queue-times.test")
        };

        await Assert.ThrowsAsync<HttpRequestException>(
            () => new QueueTimesClient(failedClient, logger)
                .GetQueueTimesForParkAsync(6, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidDataException>(
            () => new QueueTimesClient(emptyClient, NullLogger<QueueTimesClient>.Instance)
                .GetQueueTimesForParkAsync(6, CancellationToken.None));

        var rejection = Assert.Single(
            logger.Entries,
            entry => entry.EventId == LogEvents.QueueTimesRequestRejected);
        Assert.Equal(LogLevel.Warning, rejection.Level);
        Assert.True(AssertProperty(rejection, "StatusCode", 500));
    }

    [Fact]
    public void QueueObservationFactory_CreatesLocalObservation()
    {
        var factory = new QueueObservationFactory();
        var collectedAt = DateTimeOffset.UtcNow;

        var observation = factory.Create(
            1,
            CreatePark(1),
            2,
            3,
            CreateRide(20, "Space Mountain"),
            collectedAt);

        Assert.Equal(1, observation.CollectionRunId);
        Assert.Equal(2, observation.LandId);
        Assert.Equal(3, observation.AttractionId);
        Assert.Equal(collectedAt, observation.CollectedAt);
        Assert.True(observation.IsOpen);
        Assert.Equal(25, observation.WaitMinutes);
    }

    [Fact]
    public void QueueObservationFactory_RejectsInvalidTimezone()
    {
        var park = CreatePark(1);
        park.Timezone = "Not/A-Timezone";

        var exception = Assert.Throws<InvalidOperationException>(
            () => new QueueObservationFactory().Create(
                1,
                park,
                null,
                3,
                CreateRide(20, "Space Mountain"),
                DateTimeOffset.UtcNow));

        Assert.Contains("invalid timezone", exception.Message);
    }

    [Fact]
    public async Task QueueCollectionJob_SkipsInvalidParksAndContinuesAfterFailure()
    {
        var logger = new TestLogger<QueueCollectionJob>();
        var parks = new FakeParksRepository(
            CreatePark(1, sourceParkId: 0),
            CreatePark(2),
            CreatePark(3));
        var collector = new FakeCollector(park =>
            park.Id == 3 ? Task.FromException(new InvalidOperationException("failed")) : Task.CompletedTask);
        var job = new QueueCollectionJob(
            parks,
            collector,
            logger);

        await job.ExecuteAsync(CancellationToken.None);

        Assert.Equal([2, 3], collector.ParkIds);
        Assert.Contains(logger.Entries, entry =>
            entry.EventId == LogEvents.ParkSkipped &&
            entry.Level == LogLevel.Warning);
        Assert.Contains(logger.Entries, entry =>
            entry.EventId == LogEvents.ParkCollectionFailed &&
            entry.Level == LogLevel.Error &&
            entry.Exception is InvalidOperationException &&
            AssertProperty(entry, "ExceptionType", nameof(InvalidOperationException)));
        var completed = Assert.Single(
            logger.Entries,
            entry => entry.EventId == LogEvents.CollectionJobCompleted);
        Assert.True(AssertProperty(completed, "SucceededCount", 1));
        Assert.True(AssertProperty(completed, "FailedCount", 1));
        Assert.True(AssertProperty(completed, "SkippedCount", 1));
    }

    [Fact]
    public async Task QueueCollectionJob_PropagatesCancellation()
    {
        var logger = new TestLogger<QueueCollectionJob>();
        using var cancellation = new CancellationTokenSource();
        var collector = new FakeCollector(_ =>
        {
            cancellation.Cancel();
            return Task.FromCanceled(cancellation.Token);
        });
        var job = new QueueCollectionJob(
            new FakeParksRepository(CreatePark(1)),
            collector,
            logger);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => job.ExecuteAsync(cancellation.Token));
        Assert.Contains(
            logger.Entries,
            entry => entry.EventId == LogEvents.CollectionJobCanceled);
    }

    [Fact]
    public async Task QueueTimesCollector_SynchronizesLandsRidesAndObservations()
    {
        var logger = new TestLogger<QueueTimesCollector>();
        var park = CreatePark(1);
        var topLevelRide = CreateRide(30, "Main Street Vehicle");
        topLevelRide.IsOpen = false;
        var provider = new FakeQueueTimesProvider(new WaitingTimeModel
        {
            Lands =
            [
                new WorkerModels.Land
                {
                    Id = 10,
                    Name = "Tomorrowland",
                    Rides = [CreateRide(20, "Space Mountain")]
                },
                new WorkerModels.Land
                {
                    Id = 11,
                    Name = "Fantasyland",
                    Rides = []
                }
            ],
            Rides =
            [
                CreateRide(20, "Duplicate Space Mountain"),
                topLevelRide
            ]
        });
        var lands = new FakeLandsRepository();
        var attractions = new FakeAttractionsRepository();
        lands.Items.Add(new Repositories.Land
        {
            Id = 5,
            ParkId = park.Id,
            SourceLandId = 10,
            Name = "Existing Tomorrowland",
            IsActive = false,
            CreatedAt = DateTimeOffset.UtcNow
        });
        lands.Items.Add(new Repositories.Land
        {
            Id = 7,
            ParkId = park.Id,
            SourceLandId = 99,
            Name = "Removed Land",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });
        lands.Items.Add(new Repositories.Land
        {
            Id = 9,
            ParkId = park.Id,
            SourceLandId = 98,
            Name = "Already Inactive Land",
            IsActive = false,
            CreatedAt = DateTimeOffset.UtcNow
        });
        attractions.Items.Add(new Attraction
        {
            Id = 6,
            ParkId = park.Id,
            SourceRideId = 20,
            Name = "Existing Space Mountain",
            IsActive = false,
            CreatedAt = DateTimeOffset.UtcNow
        });
        attractions.Items.Add(new Attraction
        {
            Id = 8,
            ParkId = park.Id,
            SourceRideId = 99,
            Name = "Removed Ride",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });
        attractions.Items.Add(new Attraction
        {
            Id = 10,
            ParkId = park.Id,
            SourceRideId = 98,
            Name = "Already Inactive Ride",
            IsActive = false,
            CreatedAt = DateTimeOffset.UtcNow
        });
        var observations = new FakeObservationsRepository();
        var runs = new FakeRunsRepository();
        var collector = new QueueTimesCollector(
            provider,
            lands,
            attractions,
            observations,
            runs,
            new QueueObservationFactory(),
            logger);

        await collector.CollectAsync(park, CancellationToken.None);

        Assert.Equal(4, lands.Items.Count);
        Assert.Equal(4, attractions.Items.Count);
        Assert.Equal(2, observations.Items.Count);
        Assert.Contains(observations.Items, observation => observation.WaitMinutes is null);
        Assert.True(lands.Items.Single(land => land.SourceLandId == 10).IsActive);
        Assert.False(lands.Items.Single(land => land.SourceLandId == 99).IsActive);
        Assert.False(lands.Items.Single(land => land.SourceLandId == 98).IsActive);
        Assert.True(attractions.Items.Single(attraction => attraction.SourceRideId == 20).IsActive);
        Assert.False(attractions.Items.Single(attraction => attraction.SourceRideId == 99).IsActive);
        Assert.False(attractions.Items.Single(attraction => attraction.SourceRideId == 98).IsActive);
        Assert.True(Assert.Single(runs.Completions).Success);
        Assert.Null(Assert.Single(runs.Completions).ErrorMessage);
        Assert.Contains(logger.Entries, entry =>
            entry.EventId == LogEvents.QueueTimesReceived &&
            AssertProperty(entry, "LandCount", 2));
        var completed = Assert.Single(
            logger.Entries,
            entry => entry.EventId == LogEvents.CollectionRunCompleted);
        Assert.True(AssertProperty(completed, "RideCount", 2));
        Assert.True(AssertProperty(completed, "DeactivatedLandCount", 1));
        Assert.True(AssertProperty(completed, "DeactivatedAttractionCount", 1));
        Assert.Equal(1, completed.Scope["CollectionRunId"]);
    }

    [Fact]
    public async Task QueueTimesCollector_MarksFailedRunAndRethrows()
    {
        var logger = new TestLogger<QueueTimesCollector>();
        var runs = new FakeRunsRepository();
        var collector = new QueueTimesCollector(
            new ThrowingQueueTimesProvider(),
            new FakeLandsRepository(),
            new FakeAttractionsRepository(),
            new FakeObservationsRepository(),
            runs,
            new QueueObservationFactory(),
            logger);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => collector.CollectAsync(CreatePark(1), CancellationToken.None));

        var completion = Assert.Single(runs.Completions);
        Assert.False(completion.Success);
        Assert.Equal("provider failed", completion.ErrorMessage);
        var failed = Assert.Single(
            logger.Entries,
            entry => entry.EventId == LogEvents.CollectionRunFailed);
        Assert.Equal(LogLevel.Error, failed.Level);
        Assert.IsType<InvalidOperationException>(failed.Exception);
    }

    [Fact]
    public async Task QueueTimesCollector_LogsCancellationAndMarksRunFailed()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var runs = new FakeRunsRepository();
        var logger = new TestLogger<QueueTimesCollector>();
        var collector = new QueueTimesCollector(
            new CancelingQueueTimesProvider(cancellation.Token),
            new FakeLandsRepository(),
            new FakeAttractionsRepository(),
            new FakeObservationsRepository(),
            runs,
            new QueueObservationFactory(),
            logger);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => collector.CollectAsync(CreatePark(1), cancellation.Token));

        Assert.False(Assert.Single(runs.Completions).Success);
        Assert.Contains(logger.Entries, entry =>
            entry.EventId == LogEvents.CollectionRunCanceled &&
            entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task Worker_StopsWhenCollectionIsCanceled()
    {
        using var cancellation = new CancellationTokenSource();
        var executionCount = 0;
        var logger = new TestLogger<WorkerModels.Worker>();
        var worker = CreateWorker(new DelegateJob(_ =>
        {
            executionCount++;
            if (executionCount == 1)
            {
                return Task.CompletedTask;
            }

            cancellation.Cancel();
            return Task.FromCanceled(cancellation.Token);
        }), logger);

        await worker.StartAsync(cancellation.Token);
        await worker.ExecuteTask!;

        Assert.True(worker.ExecuteTask.IsCompletedSuccessfully);
        Assert.Equal(2, executionCount);
        Assert.Contains(logger.Entries, entry =>
            entry.EventId == LogEvents.CollectionCycleCompleted);
        Assert.Contains(logger.Entries, entry =>
            entry.EventId == LogEvents.WorkerStopping);
        Assert.All(
            logger.Entries.Where(entry => entry.EventId == LogEvents.CollectionCycleStarted),
            entry => Assert.True(entry.Scope.ContainsKey("CollectionCycleId")));
    }

    [Fact]
    public async Task Worker_LogsUnexpectedFailureAndStopsGracefully()
    {
        using var cancellation = new CancellationTokenSource();
        var logger = new TestLogger<WorkerModels.Worker>();
        var worker = CreateWorker(new DelegateJob(_ =>
        {
            cancellation.Cancel();
            return Task.FromException(new InvalidOperationException("cycle failed"));
        }), logger);

        await worker.StartAsync(cancellation.Token);
        await worker.ExecuteTask!;

        var failed = Assert.Single(
            logger.Entries,
            entry => entry.EventId == LogEvents.CollectionCycleFailed);
        Assert.Equal(LogLevel.Error, failed.Level);
        Assert.IsType<InvalidOperationException>(failed.Exception);
    }

    private static WorkerModels.Worker CreateWorker(
        IQueueCollectionJob job,
        ILogger<WorkerModels.Worker>? logger = null)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => job);
        var provider = services.BuildServiceProvider();

        return new WorkerModels.Worker(
            logger ?? NullLogger<WorkerModels.Worker>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new QueueCollectionOptions { Interval = TimeSpan.Zero }));
    }

    private static bool AssertProperty<T>(
        LogEntry entry,
        string name,
        T expected) =>
        entry.Properties.TryGetValue(name, out var value) &&
        Equals(value, expected);

    private static Park CreatePark(int id, int sourceParkId = 6) =>
        new()
        {
            Id = id,
            SourceParkId = sourceParkId,
            Name = $"Park {id}",
            Timezone = "UTC"
        };

    private static Ride CreateRide(int id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            IsOpen = true,
            WaitTime = 25,
            LastUpdated = new DateTimeOffset(2026, 8, 18, 18, 0, 0, TimeSpan.Zero)
        };

    private sealed class StubHttpMessageHandler(HttpStatusCode statusCode, string content)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Contains("/parks/6/queue_times.json", request.RequestUri!.ToString());

            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class FakeParksRepository(params Park[] parks) : IParksRepository
    {
        public IReadOnlyList<Park> GetAll() => parks;
    }

    private sealed class FakeCollector(Func<Park, Task> collect) : IQueueTimesCollector
    {
        public List<int> ParkIds { get; } = [];

        public async Task CollectAsync(Park park, CancellationToken cancellationToken)
        {
            ParkIds.Add(park.Id);
            await collect(park);
        }
    }

    private sealed class FakeQueueTimesProvider(WaitingTimeModel result) : IQueueTimesProvider
    {
        public Task<WaitingTimeModel> GetQueueTimesForParkAsync(
            int sourceParkId,
            CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private sealed class ThrowingQueueTimesProvider : IQueueTimesProvider
    {
        public Task<WaitingTimeModel> GetQueueTimesForParkAsync(
            int sourceParkId,
            CancellationToken cancellationToken) =>
            Task.FromException<WaitingTimeModel>(
                new InvalidOperationException("provider failed"));
    }

    private sealed class CancelingQueueTimesProvider : IQueueTimesProvider
    {
        private readonly CancellationToken _cancellationToken;

        public CancelingQueueTimesProvider(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
        }

        public Task<WaitingTimeModel> GetQueueTimesForParkAsync(
            int sourceParkId,
            CancellationToken cancellationToken) =>
            Task.FromCanceled<WaitingTimeModel>(_cancellationToken);
    }

    private sealed class FakeLandsRepository : ILandsRepository
    {
        public List<Repositories.Land> Items { get; } = [];

        public IReadOnlyList<Repositories.Land> GetByParkId(int parkId) =>
            Items.Where(item => item.ParkId == parkId).ToList();

        public Repositories.Land Upsert(Repositories.Land entity)
        {
            if (entity.Id == 0)
            {
                entity.Id = Items.Count + 1;
                entity.CreatedAt = DateTimeOffset.UtcNow;
                Items.Add(entity);
            }
            else
            {
                Items[Items.FindIndex(item => item.Id == entity.Id)] = entity;
            }

            entity.UpdatedAt = DateTimeOffset.UtcNow;
            return entity;
        }
    }

    private sealed class FakeAttractionsRepository : IAttractionsRepository
    {
        public List<Attraction> Items { get; } = [];

        public IReadOnlyList<Attraction> GetByParkId(int parkId) =>
            Items.Where(item => item.ParkId == parkId).ToList();

        public Attraction Upsert(Attraction entity)
        {
            if (entity.Id == 0)
            {
                entity.Id = Items.Count + 1;
                entity.CreatedAt = DateTimeOffset.UtcNow;
                Items.Add(entity);
            }
            else
            {
                Items[Items.FindIndex(item => item.Id == entity.Id)] = entity;
            }

            entity.UpdatedAt = DateTimeOffset.UtcNow;
            return entity;
        }
    }

    private sealed class FakeObservationsRepository : IQueueObservationsRepository
    {
        public List<QueueObservation> Items { get; } = [];

        public QueueObservation Upsert(QueueObservation entity)
        {
            entity.Id = Items.Count + 1;
            Items.Add(entity);
            return entity;
        }
    }

    private sealed class FakeRunsRepository : IQueueCollectionRunsRepository
    {
        public List<(int Id, bool Success, string? ErrorMessage)> Completions { get; } = [];

        public QueueCollectionRun Start(int parkId, DateTimeOffset startedAt) =>
            new()
            {
                Id = 1,
                ParkId = parkId,
                StartedAt = startedAt
            };

        public void Complete(
            int id,
            DateTimeOffset completedAt,
            bool success,
            string? errorMessage = null) =>
            Completions.Add((id, success, errorMessage));
    }

    private sealed class DelegateJob(Func<CancellationToken, Task> execute) : IQueueCollectionJob
    {
        public Task ExecuteAsync(CancellationToken cancellationToken) => execute(cancellationToken);
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        private readonly Stack<IReadOnlyDictionary<string, object?>> _scopes = new();

        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            var scope = ToDictionary(state);
            _scopes.Push(scope);
            return new Scope(() => _scopes.Pop());
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var scope = _scopes
                .Reverse()
                .SelectMany(item => item)
                .ToDictionary(item => item.Key, item => item.Value);
            Entries.Add(new LogEntry(
                logLevel,
                eventId,
                formatter(state, exception),
                exception,
                ToDictionary(state),
                scope));
        }

        private static IReadOnlyDictionary<string, object?> ToDictionary<TState>(TState state) =>
            state is IEnumerable<KeyValuePair<string, object?>> properties
                ? properties.ToDictionary(item => item.Key, item => item.Value)
                : new Dictionary<string, object?> { ["Scope"] = state };

        private sealed class Scope(Action dispose) : IDisposable
        {
            public void Dispose() => dispose();
        }
    }

    private sealed record LogEntry(
        LogLevel Level,
        EventId EventId,
        string Message,
        Exception? Exception,
        IReadOnlyDictionary<string, object?> Properties,
        IReadOnlyDictionary<string, object?> Scope);
}
