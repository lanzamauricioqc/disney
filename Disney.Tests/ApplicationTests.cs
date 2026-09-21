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
    public async Task AnalyticsService_QueriesHistoricalObservationsForRequestedWindow()
    {
        var from = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        var to = from.AddDays(7);
        var analyticsReader = new FakeQueueAnalyticsReader();
        var service = new QueueAnalyticsService(
            analyticsReader,
            new FixedTimeProvider(DateTimeOffset.UtcNow));

        var result = await service.GetHistoricalWaitTimesAsync(
            1,
            20,
            from,
            to,
            CancellationToken.None);

        Assert.Equal(from, result.FromInclusive);
        Assert.Equal(to, result.ToExclusive);
        Assert.Equal(from, analyticsReader.FromInclusive);
        Assert.Equal(to, analyticsReader.ToExclusive);
    }

    [Fact]
    public async Task AnalyticsService_QueriesSelectedParkComparisonWeek()
    {
        var currentTime = new DateTimeOffset(2026, 8, 26, 13, 0, 0, TimeSpan.Zero);
        var analyticsReader = new FakeQueueAnalyticsReader();
        var service = new QueueAnalyticsService(
            analyticsReader,
            new FixedTimeProvider(currentTime));
        var weekStart = new DateOnly(2026, 8, 17);

        var result = await service.GetDailyParkWaitTimesAsync(
            weekStart,
            CancellationToken.None);

        Assert.Equal(weekStart, result.WeekStart);
        Assert.Equal(new DateOnly(2026, 8, 23), result.WeekEnd);
        Assert.Equal(new DateOnly(2026, 5, 26), result.AvailableFrom);
        Assert.Equal(new DateOnly(2026, 8, 24), result.CurrentWeekStart);
        Assert.Equal(weekStart, analyticsReader.DailyParksFromInclusive);
        Assert.Equal(new DateOnly(2026, 8, 24), analyticsReader.DailyParksToExclusive);
    }

    [Fact]
    public async Task AnalyticsService_RejectsInvalidParkComparisonWeeks()
    {
        var service = new QueueAnalyticsService(
            new FakeQueueAnalyticsReader(),
            new FixedTimeProvider(
                new DateTimeOffset(2026, 8, 26, 13, 0, 0, TimeSpan.Zero)));

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetDailyParkWaitTimesAsync(
                new DateOnly(2026, 8, 25),
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.GetDailyParkWaitTimesAsync(
                new DateOnly(2026, 8, 31),
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.GetDailyParkWaitTimesAsync(
                new DateOnly(2026, 5, 11),
                CancellationToken.None));
    }

    [Fact]
    public async Task AnalyticsService_RejectsInvalidHistoricalWindow()
    {
        var from = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        var service = new QueueAnalyticsService(
            new FakeQueueAnalyticsReader(),
            new FixedTimeProvider(DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetHistoricalWaitTimesAsync(
                1,
                20,
                from,
                from,
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.GetHistoricalWaitTimesAsync(
                1,
                20,
                from,
                from.AddDays(32),
                CancellationToken.None));
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

    [Fact]
    public async Task PredictionService_UsesHistoricalMedianForTargetWindow()
    {
        var currentTime = new DateTimeOffset(2026, 9, 18, 14, 0, 0, TimeSpan.Zero);
        var predictionReader = new FakeQueuePredictionReader(
            new QueuePredictionData(20, "Space Mountain", [15, 30, 45, 60]));
        var service = new QueuePredictionService(
            predictionReader,
            new FixedTimeProvider(currentTime));
        var targetAt = currentTime.AddHours(2);

        var result = await service.PredictWaitTimeAsync(
            1,
            20,
            targetAt,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(WaitTimePredictionStatus.Available, result.Status);
        Assert.Equal((short)38, result.PredictedWaitMinutes);
        Assert.Equal(0.20m, result.ConfidenceScore);
        Assert.Equal(4, result.HistoricalSampleCount);
        Assert.Equal("weekday-quarter-hour-median-v1", result.AlgorithmVersion);
        Assert.Equal(currentTime.AddMonths(-3), predictionReader.WindowStart);
        Assert.Equal(currentTime, predictionReader.WindowEnd);
        Assert.Equal(targetAt, predictionReader.TargetAt);
    }

    [Fact]
    public async Task PredictionService_ReportsInsufficientHistoricalData()
    {
        var currentTime = new DateTimeOffset(2026, 9, 18, 14, 0, 0, TimeSpan.Zero);
        var service = new QueuePredictionService(
            new FakeQueuePredictionReader(
                new QueuePredictionData(20, "Space Mountain", [25, 30])),
            new FixedTimeProvider(currentTime));

        var result = await service.PredictWaitTimeAsync(
            1,
            20,
            currentTime.AddHours(1),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(WaitTimePredictionStatus.InsufficientHistoricalData, result.Status);
        Assert.Null(result.PredictedWaitMinutes);
        Assert.Null(result.ConfidenceScore);
        Assert.Equal(2, result.HistoricalSampleCount);
    }

    [Fact]
    public async Task PredictionService_ReportsFullConfidenceForConsistentHistory()
    {
        var currentTime = new DateTimeOffset(2026, 9, 18, 14, 0, 0, TimeSpan.Zero);
        var service = new QueuePredictionService(
            new FakeQueuePredictionReader(
                new QueuePredictionData(
                    20,
                    "Space Mountain",
                    Enumerable.Repeat((short)30, 12).ToArray())),
            new FixedTimeProvider(currentTime));

        var result = await service.PredictWaitTimeAsync(
            1,
            20,
            currentTime.AddHours(1),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(1m, result.ConfidenceScore);
    }

    [Fact]
    public async Task PredictionService_ReportsZeroConfidenceForHighlyVariableHistory()
    {
        var currentTime = new DateTimeOffset(2026, 9, 18, 14, 0, 0, TimeSpan.Zero);
        var service = new QueuePredictionService(
            new FakeQueuePredictionReader(
                new QueuePredictionData(20, "Space Mountain", [0, 0, 180])),
            new FixedTimeProvider(currentTime));

        var result = await service.PredictWaitTimeAsync(
            1,
            20,
            currentTime.AddHours(1),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(0m, result.ConfidenceScore);
    }

    [Fact]
    public async Task PredictionService_ReturnsNullForUnknownAttraction()
    {
        var currentTime = new DateTimeOffset(2026, 9, 18, 14, 0, 0, TimeSpan.Zero);
        var service = new QueuePredictionService(
            new FakeQueuePredictionReader(null),
            new FixedTimeProvider(currentTime));

        var result = await service.PredictWaitTimeAsync(
            1,
            20,
            currentTime.AddHours(1),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task PredictionService_RejectsInvalidRequests()
    {
        var currentTime = new DateTimeOffset(2026, 9, 18, 14, 0, 0, TimeSpan.Zero);
        var service = new QueuePredictionService(
            new FakeQueuePredictionReader(null),
            new FixedTimeProvider(currentTime));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.PredictWaitTimeAsync(
                0,
                20,
                currentTime.AddHours(1),
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.PredictWaitTimeAsync(
                1,
                0,
                currentTime.AddHours(1),
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.PredictWaitTimeAsync(
                1,
                20,
                currentTime,
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.PredictWaitTimeAsync(
                1,
                20,
                currentTime.Add(QueuePredictionService.MaximumPredictionHorizon)
                    .AddMinutes(1),
                CancellationToken.None));
    }

    [Fact]
    public async Task WalkingTimeService_EstimatesRouteDistanceAndDuration()
    {
        var service = new WalkingTimeService(new FakeWalkingTimeReader(
            new WalkingTimeData(
                10,
                "Origin",
                0m,
                0m,
                20,
                "Destination",
                0m,
                0.008993m)));

        var result = await service.EstimateAsync(1, 10, 20, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(WalkingTimeEstimateStatus.Available, result.Status);
        Assert.InRange(result.DirectDistanceMeters!.Value, 999, 1001);
        Assert.InRange(result.EstimatedRouteDistanceMeters!.Value, 1249, 1251);
        Assert.Equal(15, result.EstimatedWalkingMinutes);
        Assert.Equal(1.25m, result.RouteDistanceMultiplier);
        Assert.Equal(1.4m, result.WalkingSpeedMetersPerSecond);
        Assert.Equal("haversine-route-factor-v1", result.AlgorithmVersion);
    }

    [Fact]
    public async Task WalkingTimeService_ReportsUnavailableCoordinates()
    {
        var service = new WalkingTimeService(new FakeWalkingTimeReader(
            new WalkingTimeData(
                10,
                "Origin",
                null,
                null,
                20,
                "Destination",
                28.4m,
                -81.5m)));

        var result = await service.EstimateAsync(1, 10, 20, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(WalkingTimeEstimateStatus.CoordinatesUnavailable, result.Status);
        Assert.Null(result.DirectDistanceMeters);
        Assert.Null(result.EstimatedRouteDistanceMeters);
        Assert.Null(result.EstimatedWalkingMinutes);
    }

    [Fact]
    public async Task WalkingTimeService_ReturnsNullForUnknownAttraction()
    {
        var service = new WalkingTimeService(new FakeWalkingTimeReader(null));

        var result = await service.EstimateAsync(1, 10, 20, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task WalkingTimeService_RejectsInvalidIdentifiers()
    {
        var service = new WalkingTimeService(new FakeWalkingTimeReader(null));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.EstimateAsync(0, 10, 20, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.EstimateAsync(1, 0, 20, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.EstimateAsync(1, 10, 0, CancellationToken.None));
    }

    [Theory]
    [InlineData(-91, 0)]
    [InlineData(91, 0)]
    [InlineData(0, -181)]
    [InlineData(0, 181)]
    public void GeoCoordinate_RejectsValuesOutsideGeographicBounds(
        int latitude,
        int longitude)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GeoCoordinate(latitude, longitude));
    }

    [Fact]
    public void GeoCoordinate_CalculatesAntipodalDistance()
    {
        var origin = new GeoCoordinate(45m, 0m);
        var destination = new GeoCoordinate(-45m, 180m);

        var distance = origin.DistanceToMeters(destination);

        Assert.InRange(distance, 20_015_000, 20_016_000);
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
        public DateTimeOffset? FromInclusive { get; private set; }
        public DateTimeOffset? ToExclusive { get; private set; }
        public DateOnly? DailyParksFromInclusive { get; private set; }
        public DateOnly? DailyParksToExclusive { get; private set; }

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

        public Task<IReadOnlyList<DailyParkWaitTime>> GetDailyParkWaitTimesAsync(
            DateOnly fromInclusive,
            DateOnly toExclusive,
            CancellationToken cancellationToken)
        {
            DailyParksFromInclusive = fromInclusive;
            DailyParksToExclusive = toExclusive;
            return Task.FromResult<IReadOnlyList<DailyParkWaitTime>>([]);
        }

        public Task<IReadOnlyList<HistoricalWaitTimeObservation>>
            GetHistoricalWaitTimesAsync(
                long parkId,
                long attractionId,
                DateTimeOffset fromInclusive,
                DateTimeOffset toExclusive,
                CancellationToken cancellationToken)
        {
            AttractionId = attractionId;
            FromInclusive = fromInclusive;
            ToExclusive = toExclusive;
            return Task.FromResult<IReadOnlyList<HistoricalWaitTimeObservation>>([]);
        }

        public Task<IReadOnlyList<WeekdayClosurePattern>> GetWeekdayClosurePatternsAsync(
            long parkId,
            long? attractionId,
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WeekdayClosurePattern>>([]);
    }

    private sealed class FakeQueuePredictionReader(QueuePredictionData? predictionData)
        : IQueuePredictionReader
    {
        public DateTimeOffset? TargetAt { get; private set; }
        public DateTimeOffset? WindowStart { get; private set; }
        public DateTimeOffset? WindowEnd { get; private set; }

        public Task<QueuePredictionData?> GetPredictionDataAsync(
            long parkId,
            long attractionId,
            DateTimeOffset targetAt,
            DateTimeOffset windowStart,
            DateTimeOffset windowEnd,
            CancellationToken cancellationToken)
        {
            TargetAt = targetAt;
            WindowStart = windowStart;
            WindowEnd = windowEnd;
            return Task.FromResult(predictionData);
        }

    }

    private sealed class FakeWalkingTimeReader(WalkingTimeData? data)
        : IWalkingTimeReader
    {
        public Task<WalkingTimeData?> GetWalkingTimeDataAsync(
            long parkId,
            long fromAttractionId,
            long toAttractionId,
            CancellationToken cancellationToken) =>
            Task.FromResult(data);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
