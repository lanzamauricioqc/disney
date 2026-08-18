using Disney.Application;
using Disney.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace Disney.Tests;

public sealed class ApplicationTests
{
    [Fact]
    public void ObservationFactory_UsesMinuteOfDayAndAuthoritativeTimestamp()
    {
        var observedAt = new DateTimeOffset(2026, 8, 18, 19, 5, 30, TimeSpan.Zero);
        var observation = new QueueObservationFactory().Create(
            1,
            CreatePark(),
            2,
            3,
            new QueueRideSnapshot(20, "Space Mountain", true, 35, observedAt),
            observedAt.AddMinutes(1));

        Assert.Equal(observedAt, observation.ObservedAt);
        Assert.Equal(15, observation.ObservedLocalHour);
        Assert.Equal(905, observation.ObservedSlotMinutes);
        Assert.Equal((short)35, observation.WaitMinutes);
    }

    [Fact]
    public void ObservationFactory_StoresNoWaitForClosedAttraction()
    {
        var observedAt = DateTimeOffset.UtcNow;
        var observation = new QueueObservationFactory().Create(
            1,
            CreatePark(),
            null,
            3,
            new QueueRideSnapshot(20, "Space Mountain", false, 35, observedAt),
            observedAt);

        Assert.Null(observation.WaitMinutes);
    }

    [Fact]
    public async Task CollectionService_PersistsSuccessfulSnapshot()
    {
        var store = new FakeStore();
        var snapshot = CreateSnapshot();
        var service = new QueueCollectionService(
            new StubProvider(snapshot),
            store,
            NullLogger<QueueCollectionService>.Instance);

        var result = await service.CollectAsync(CreatePark(), CancellationToken.None);

        Assert.Equal(42, result.CollectionRunId);
        Assert.Same(snapshot, store.Snapshot);
        Assert.Null(store.Failure);
    }

    [Fact]
    public async Task CollectionService_MarksFailedRun()
    {
        var store = new FakeStore();
        var service = new QueueCollectionService(
            new ThrowingProvider(),
            store,
            NullLogger<QueueCollectionService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CollectAsync(CreatePark(), CancellationToken.None));

        Assert.Equal("provider failed", store.Failure);
    }

    [Fact]
    public async Task CollectionJob_ContinuesAfterOneParkFails()
    {
        var service = new FakeCollectionService();
        var job = new QueueCollectionJob(
            new StubParkReader(
                CreatePark(1, 6),
                CreatePark(2, 0),
                CreatePark(3, 7)),
            service,
            NullLogger<QueueCollectionJob>.Instance);

        await job.ExecuteAsync(CancellationToken.None);

        Assert.Equal([1L, 3L], service.ParkIds);
    }

    [Fact]
    public async Task AnalyticsService_UsesTrailingThreeMonthWindow()
    {
        var now = new DateTimeOffset(2026, 8, 18, 22, 0, 0, TimeSpan.Zero);
        var reader = new FakeAnalyticsReader();
        var service = new QueueAnalyticsService(reader, new FixedTimeProvider(now));

        var current = await service.GetCurrentWaitTimesAsync(1, CancellationToken.None);
        var waits = await service.GetWeekdayWaitTimePatternsAsync(
            1,
            20,
            CancellationToken.None);
        var closures = await service.GetWeekdayClosurePatternsAsync(
            1,
            null,
            CancellationToken.None);

        Assert.Equal(now.AddMonths(-3), current.WindowStart);
        Assert.Equal(now, current.GeneratedAt);
        Assert.Equal(now.AddMonths(-3), waits.WindowStart);
        Assert.Equal(now, waits.WindowEnd);
        Assert.Equal(now.AddMonths(-3), closures.WindowStart);
        Assert.Equal(20, reader.AttractionId);
    }

    [Fact]
    public async Task AnalyticsService_RejectsInvalidIdentifiers()
    {
        var service = new QueueAnalyticsService(
            new FakeAnalyticsReader(),
            new FixedTimeProvider(DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.GetCurrentWaitTimesAsync(0, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.GetWeekdayWaitTimePatternsAsync(
                1,
                0,
                CancellationToken.None));
    }

    private static Park CreatePark(long id = 1, int sourceId = 6) =>
        new()
        {
            Id = id,
            SourceParkId = sourceId,
            Name = $"Park {id}",
            Timezone = "America/New_York"
        };

    private static QueueTimesSnapshot CreateSnapshot() =>
        new(
            [new QueueLandSnapshot(10, "Tomorrowland", [])],
            [new QueueRideSnapshot(20, "Space Mountain", true, 25, DateTimeOffset.UtcNow)]);

    private sealed class StubProvider(QueueTimesSnapshot snapshot) : IQueueTimesProvider
    {
        public Task<QueueTimesSnapshot> GetQueueTimesForParkAsync(
            int sourceParkId,
            CancellationToken cancellationToken) => Task.FromResult(snapshot);
    }

    private sealed class ThrowingProvider : IQueueTimesProvider
    {
        public Task<QueueTimesSnapshot> GetQueueTimesForParkAsync(
            int sourceParkId,
            CancellationToken cancellationToken) =>
            Task.FromException<QueueTimesSnapshot>(
                new InvalidOperationException("provider failed"));
    }

    private sealed class FakeStore : IQueueCollectionStore
    {
        public QueueTimesSnapshot? Snapshot { get; private set; }
        public string? Failure { get; private set; }

        public Task<long> StartRunAsync(
            long parkId,
            DateTimeOffset startedAt,
            CancellationToken cancellationToken) => Task.FromResult(42L);

        public Task<CollectionResult> PersistSuccessfulRunAsync(
            long runId,
            Park park,
            QueueTimesSnapshot snapshot,
            DateTimeOffset collectedAt,
            CancellationToken cancellationToken)
        {
            Snapshot = snapshot;
            return Task.FromResult(new CollectionResult(runId, 1, 1, 1, 0, 0));
        }

        public Task FailRunAsync(
            long runId,
            DateTimeOffset completedAt,
            string errorMessage,
            CancellationToken cancellationToken)
        {
            Failure = errorMessage;
            return Task.CompletedTask;
        }
    }

    private sealed class StubParkReader(params Park[] parks) : IParkReader
    {
        public Task<IReadOnlyList<Park>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Park>>(parks);
    }

    private sealed class FakeCollectionService : IQueueCollectionService
    {
        public List<long> ParkIds { get; } = [];

        public Task<CollectionResult> CollectAsync(Park park, CancellationToken cancellationToken)
        {
            ParkIds.Add(park.Id);
            if (park.Id == 1)
            {
                throw new InvalidOperationException("expected");
            }

            return Task.FromResult(new CollectionResult(1, 0, 0, 0, 0, 0));
        }
    }

    private sealed class FakeAnalyticsReader : IQueueAnalyticsReader
    {
        public long? AttractionId { get; private set; }

        public Task<IReadOnlyList<CurrentWaitTime>> GetCurrentWaitTimesAsync(
            long parkId,
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CurrentWaitTime>>([]);

        public Task<IReadOnlyList<WeekdayWaitTimePattern>> GetWeekdayWaitTimePatternsAsync(
            long parkId,
            long? attractionId,
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken)
        {
            AttractionId = attractionId;
            return Task.FromResult<IReadOnlyList<WeekdayWaitTimePattern>>([]);
        }

        public Task<IReadOnlyList<WeekdayClosurePattern>> GetWeekdayClosurePatternsAsync(
            long parkId,
            long? attractionId,
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WeekdayClosurePattern>>([]);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
