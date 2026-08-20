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
        Assert.Equal(new DateOnly(2026, 8, 18), observation.ObservedUtcDate);
        Assert.Equal(new TimeOnly(19, 5, 30), observation.ObservedUtcTime);
        Assert.Equal(19, observation.ObservedUtcHour);
        Assert.Equal(1145, observation.ObservedUtcSlotMinutes);
        Assert.Equal((short)DayOfWeek.Tuesday, observation.ObservedUtcDayOfWeek);
        Assert.Equal(15, observation.ObservedLocalHour);
        Assert.Equal(905, observation.ObservedSlotMinutes);
        Assert.Equal((short)35, observation.WaitMinutes);
    }

    [Fact]
    public void ObservationFactory_NormalizesOffsetTimestampForUtcComponents()
    {
        var observedAt =
            new DateTimeOffset(2026, 8, 18, 15, 5, 30, TimeSpan.FromHours(-4));
        var observation = new QueueObservationFactory().Create(
            1,
            CreatePark(),
            2,
            3,
            new QueueRideSnapshot(20, "Space Mountain", true, 35, observedAt),
            observedAt);

        Assert.Equal(new DateOnly(2026, 8, 18), observation.ObservedUtcDate);
        Assert.Equal(new TimeOnly(19, 5, 30), observation.ObservedUtcTime);
        Assert.Equal(19, observation.ObservedUtcHour);
        Assert.Equal(1145, observation.ObservedUtcSlotMinutes);
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
        var collectionStore = new FakeQueueCollectionStore();
        var snapshot = CreateSnapshot();
        var service = new QueueCollectionService(
            new StubQueueTimesProvider(snapshot),
            collectionStore,
            NullLogger<QueueCollectionService>.Instance);

        var collectionResult = await service.CollectAsync(
            CreatePark(),
            CancellationToken.None);

        Assert.Equal(42, collectionResult.CollectionRunId);
        Assert.Same(snapshot, collectionStore.Snapshot);
        Assert.Null(collectionStore.Failure);
    }

    [Fact]
    public async Task CollectionService_MarksFailedRun()
    {
        var collectionStore = new FakeQueueCollectionStore();
        var service = new QueueCollectionService(
            new ThrowingQueueTimesProvider(),
            collectionStore,
            NullLogger<QueueCollectionService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CollectAsync(CreatePark(), CancellationToken.None));

        Assert.Equal("provider failed", collectionStore.Failure);
    }

    [Fact]
    public async Task CollectionJob_ContinuesAfterOneParkFails()
    {
        var collectionService = new FakeQueueCollectionService();
        var collectionJob = new QueueCollectionJob(
            new StubParkReader(
                CreatePark(1, 6),
                CreatePark(2, 0),
                CreatePark(3, 7)),
            collectionService,
            NullLogger<QueueCollectionJob>.Instance);

        await collectionJob.ExecuteAsync(CancellationToken.None);

        Assert.Equal([1L, 3L], collectionService.ParkIds);
    }

    [Fact]
    public async Task CollectionJob_RespectsParkCollectionControls()
    {
        var collectionService = new FakeQueueCollectionService();
        var collectionJob = new QueueCollectionJob(
            new StubParkReader(
                CreatePark(1, collectionEnabled: false),
                CreatePark(
                    2,
                    lastCollectionStartedAt: DateTimeOffset.UtcNow,
                    collectionIntervalMinutes: 5),
                CreatePark(
                    3,
                    lastCollectionStartedAt: DateTimeOffset.UtcNow.AddMinutes(-6),
                    collectionIntervalMinutes: 5)),
            collectionService,
            NullLogger<QueueCollectionJob>.Instance);

        await collectionJob.ExecuteAsync(CancellationToken.None);

        Assert.Equal([3L], collectionService.ParkIds);
    }

    [Fact]
    public async Task AnalyticsService_UsesTrailingThreeMonthWindow()
    {
        var currentTime = new DateTimeOffset(2026, 8, 18, 22, 0, 0, TimeSpan.Zero);
        var analyticsReader = new FakeQueueAnalyticsReader();
        var service = new QueueAnalyticsService(
            analyticsReader,
            new FixedTimeProvider(currentTime));

        var currentWaitTimes = await service.GetCurrentWaitTimesAsync(
            1,
            CancellationToken.None);
        var waitTimePatterns = await service.GetWeekdayWaitTimePatternsAsync(
            1,
            20,
            CancellationToken.None);
        var dailyHistory = await service.GetDailyWaitTimeHistoryAsync(
            1,
            20,
            CancellationToken.None);
        var closurePatterns = await service.GetWeekdayClosurePatternsAsync(
            1,
            null,
            CancellationToken.None);

        Assert.Equal(currentTime.AddMonths(-3), currentWaitTimes.WindowStart);
        Assert.Equal(currentTime, currentWaitTimes.GeneratedAt);
        Assert.Equal(currentTime.AddMonths(-3), waitTimePatterns.WindowStart);
        Assert.Equal(currentTime, waitTimePatterns.WindowEnd);
        Assert.Equal(currentTime.AddMonths(-3), dailyHistory.WindowStart);
        Assert.Equal(currentTime, dailyHistory.WindowEnd);
        Assert.Equal(currentTime.AddMonths(-3), closurePatterns.WindowStart);
        Assert.Equal(20, analyticsReader.AttractionId);
    }

    [Fact]
    public async Task AnalyticsService_RejectsInvalidIdentifiers()
    {
        var service = new QueueAnalyticsService(
            new FakeQueueAnalyticsReader(),
            new FixedTimeProvider(DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.GetCurrentWaitTimesAsync(0, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.GetWeekdayWaitTimePatternsAsync(
                1,
                0,
                CancellationToken.None));
    }

    private static Park CreatePark(
        long id = 1,
        int sourceId = 6,
        bool collectionEnabled = true,
        int collectionIntervalMinutes = 5,
        DateTimeOffset? lastCollectionStartedAt = null) =>
        new()
        {
            Id = id,
            SourceParkId = sourceId,
            Name = $"Park {id}",
            Timezone = "America/New_York",
            CollectionEnabled = collectionEnabled,
            CollectionIntervalMinutes = collectionIntervalMinutes,
            LastCollectionStartedAt = lastCollectionStartedAt
        };

    private static QueueTimesSnapshot CreateSnapshot() =>
        new(
            [new QueueLandSnapshot(10, "Tomorrowland", [])],
            [new QueueRideSnapshot(20, "Space Mountain", true, 25, DateTimeOffset.UtcNow)]);

    private sealed class StubQueueTimesProvider(QueueTimesSnapshot snapshot)
        : IQueueTimesProvider
    {
        public Task<QueueTimesSnapshot> GetQueueTimesForParkAsync(
            int sourceParkId,
            CancellationToken cancellationToken) => Task.FromResult(snapshot);
    }

    private sealed class ThrowingQueueTimesProvider : IQueueTimesProvider
    {
        public Task<QueueTimesSnapshot> GetQueueTimesForParkAsync(
            int sourceParkId,
            CancellationToken cancellationToken) =>
            Task.FromException<QueueTimesSnapshot>(
                new InvalidOperationException("provider failed"));
    }

    private sealed class FakeQueueCollectionStore : IQueueCollectionStore
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

    private sealed class FakeQueueCollectionService : IQueueCollectionService
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

    private sealed class FakeQueueAnalyticsReader : IQueueAnalyticsReader
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

        public Task<IReadOnlyList<DailyWaitTimeHistory>> GetDailyWaitTimeHistoryAsync(
            long parkId,
            long attractionId,
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken)
        {
            AttractionId = attractionId;
            return Task.FromResult<IReadOnlyList<DailyWaitTimeHistory>>([]);
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
