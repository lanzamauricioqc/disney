using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
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
        var client = new QueueTimesClient(httpClient, NullLogger<QueueTimesClient>.Instance);

        var result = await client.GetQueueTimesForParkAsync(
            6,
            CancellationToken.None);

        Assert.Equal("Space Mountain", Assert.Single(Assert.Single(result.Lands).Rides).Name);
    }

    [Fact]
    public async Task QueueTimesClient_RejectsFailuresAndEmptyPayloads()
    {
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
            () => new QueueTimesClient(failedClient, NullLogger<QueueTimesClient>.Instance)
                .GetQueueTimesForParkAsync(6, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidDataException>(
            () => new QueueTimesClient(emptyClient, NullLogger<QueueTimesClient>.Instance)
                .GetQueueTimesForParkAsync(6, CancellationToken.None));
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
        var parks = new FakeParksRepository(
            CreatePark(1, sourceParkId: 0),
            CreatePark(2),
            CreatePark(3));
        var collector = new FakeCollector(park =>
            park.Id == 3 ? Task.FromException(new InvalidOperationException("failed")) : Task.CompletedTask);
        var job = new QueueCollectionJob(
            parks,
            collector,
            NullLogger<QueueCollectionJob>.Instance);

        await job.ExecuteAsync(CancellationToken.None);

        Assert.Equal([2, 3], collector.ParkIds);
    }

    [Fact]
    public async Task QueueCollectionJob_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var collector = new FakeCollector(_ =>
        {
            cancellation.Cancel();
            return Task.FromCanceled(cancellation.Token);
        });
        var job = new QueueCollectionJob(
            new FakeParksRepository(CreatePark(1)),
            collector,
            NullLogger<QueueCollectionJob>.Instance);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => job.ExecuteAsync(cancellation.Token));
    }

    [Fact]
    public async Task QueueTimesCollector_SynchronizesLandsRidesAndObservations()
    {
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
            CreatedAt = DateTimeOffset.UtcNow
        });
        attractions.Items.Add(new Attraction
        {
            Id = 6,
            ParkId = park.Id,
            SourceRideId = 20,
            Name = "Existing Space Mountain",
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
            NullLogger<QueueTimesCollector>.Instance);

        await collector.CollectAsync(park, CancellationToken.None);

        Assert.Equal(2, lands.Items.Count);
        Assert.Equal(2, attractions.Items.Count);
        Assert.Equal(2, observations.Items.Count);
        Assert.Contains(observations.Items, observation => observation.WaitMinutes is null);
        Assert.True(Assert.Single(runs.Completions).Success);
        Assert.Null(Assert.Single(runs.Completions).ErrorMessage);
    }

    [Fact]
    public async Task QueueTimesCollector_MarksFailedRunAndRethrows()
    {
        var runs = new FakeRunsRepository();
        var collector = new QueueTimesCollector(
            new ThrowingQueueTimesProvider(),
            new FakeLandsRepository(),
            new FakeAttractionsRepository(),
            new FakeObservationsRepository(),
            runs,
            new QueueObservationFactory(),
            NullLogger<QueueTimesCollector>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => collector.CollectAsync(CreatePark(1), CancellationToken.None));

        var completion = Assert.Single(runs.Completions);
        Assert.False(completion.Success);
        Assert.Equal("provider failed", completion.ErrorMessage);
    }

    [Fact]
    public async Task Worker_StopsWhenCollectionIsCanceled()
    {
        using var cancellation = new CancellationTokenSource();
        var executionCount = 0;
        var worker = CreateWorker(new DelegateJob(_ =>
        {
            executionCount++;
            if (executionCount == 1)
            {
                return Task.CompletedTask;
            }

            cancellation.Cancel();
            return Task.FromCanceled(cancellation.Token);
        }));

        await worker.StartAsync(cancellation.Token);
        await worker.ExecuteTask!;

        Assert.True(worker.ExecuteTask.IsCompletedSuccessfully);
        Assert.Equal(2, executionCount);
    }

    [Fact]
    public async Task Worker_LogsUnexpectedFailureUntilCancellationStopsDelay()
    {
        using var cancellation = new CancellationTokenSource();
        var worker = CreateWorker(new DelegateJob(_ =>
        {
            cancellation.Cancel();
            return Task.FromException(new InvalidOperationException("cycle failed"));
        }));

        await worker.StartAsync(cancellation.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker.ExecuteTask!);
    }

    private static WorkerModels.Worker CreateWorker(IQueueCollectionJob job)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => job);
        var provider = services.BuildServiceProvider();

        return new WorkerModels.Worker(
            NullLogger<WorkerModels.Worker>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new QueueCollectionOptions { Interval = TimeSpan.Zero }));
    }

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
}
