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
        Assert.Equal(WalkingRouteSource.CoordinateEstimate, result.RouteSource);
        Assert.Null(result.RouteNodeIds);
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
        Assert.Null(result.RouteSource);
        Assert.Null(result.RouteNodeIds);
    }

    [Fact]
    public async Task WalkingTimeService_UsesShortestParkGraphRoute()
    {
        var service = new WalkingTimeService(new FakeWalkingTimeReader(
            new WalkingTimeData(
                10,
                "Origin",
                28.4m,
                -81.5m,
                20,
                "Destination",
                28.41m,
                -81.49m,
                1,
                4,
                [1, 2, 3, 4],
                [
                    new WalkableRouteEdge(1, 2, 100),
                    new WalkableRouteEdge(2, 4, 300),
                    new WalkableRouteEdge(1, 3, 120),
                    new WalkableRouteEdge(3, 4, 150)
                ])));

        var result = await service.EstimateAsync(1, 10, 20, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(WalkingTimeEstimateStatus.Available, result.Status);
        Assert.Equal(270, result.EstimatedRouteDistanceMeters);
        Assert.Equal(4, result.EstimatedWalkingMinutes);
        Assert.Equal(WalkingRouteSource.ParkGraph, result.RouteSource);
        Assert.Equal([1L, 3L, 4L], result.RouteNodeIds);
        Assert.Equal(1m, result.RouteDistanceMultiplier);
        Assert.Equal("park-graph-dijkstra-v1", result.AlgorithmVersion);
    }

    [Fact]
    public async Task WalkingTimeService_UsesGraphWithoutAttractionCoordinates()
    {
        var service = new WalkingTimeService(new FakeWalkingTimeReader(
            new WalkingTimeData(
                10,
                "Origin",
                null,
                null,
                20,
                "Destination",
                null,
                null,
                1,
                2,
                [1, 2],
                [new WalkableRouteEdge(1, 2, 84)])));

        var result = await service.EstimateAsync(1, 10, 20, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(WalkingTimeEstimateStatus.Available, result.Status);
        Assert.Null(result.DirectDistanceMeters);
        Assert.Equal(84, result.EstimatedRouteDistanceMeters);
        Assert.Equal(1, result.EstimatedWalkingMinutes);
    }

    [Fact]
    public async Task WalkingTimeService_ReportsUnavailableDisconnectedRoute()
    {
        var service = new WalkingTimeService(new FakeWalkingTimeReader(
            new WalkingTimeData(
                10,
                "Origin",
                28.4m,
                -81.5m,
                20,
                "Destination",
                28.41m,
                -81.49m,
                1,
                2,
                [1, 2],
                [])));

        var result = await service.EstimateAsync(1, 10, 20, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(WalkingTimeEstimateStatus.RouteUnavailable, result.Status);
        Assert.NotNull(result.DirectDistanceMeters);
        Assert.Null(result.EstimatedRouteDistanceMeters);
        Assert.Equal(WalkingRouteSource.ParkGraph, result.RouteSource);
        Assert.Equal(1m, result.RouteDistanceMultiplier);
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

    [Fact]
    public void WalkableParkGraph_ReturnsZeroDistanceForSameNode()
    {
        var graph = new WalkableParkGraph([1], []);

        var route = graph.FindShortestRoute(1, 1);

        Assert.NotNull(route);
        Assert.Equal([1L], route.NodeIds);
        Assert.Equal(0, route.DistanceMeters);
    }

    [Fact]
    public void WalkableParkGraph_ReturnsNullForUnknownOrDisconnectedNodes()
    {
        var graph = new WalkableParkGraph([1, 2], []);

        Assert.Null(graph.FindShortestRoute(1, 2));
        Assert.Null(graph.FindShortestRoute(1, 3));
    }

    [Fact]
    public void WalkableParkGraph_UsesStableNodeOrderForEqualRoutes()
    {
        var graph = new WalkableParkGraph(
            [1, 2, 3, 4],
            [
                new WalkableRouteEdge(1, 3, 10),
                new WalkableRouteEdge(3, 4, 10),
                new WalkableRouteEdge(1, 2, 10),
                new WalkableRouteEdge(2, 4, 10)
            ]);

        var route = graph.FindShortestRoute(1, 4);

        Assert.NotNull(route);
        Assert.Equal([1L, 2L, 4L], route.NodeIds);
    }

    [Fact]
    public void WalkableParkGraph_RejectsInvalidGraphData()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new WalkableParkGraph([0], []));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new WalkableParkGraph(
                [1, 2],
                [new WalkableRouteEdge(1, 2, 0)]));
        Assert.Throws<ArgumentException>(
            () => new WalkableParkGraph(
                [1],
                [new WalkableRouteEdge(1, 2, 10)]));
    }

    [Fact]
    public async Task ItineraryOptimizer_GeneratesDeterministicOrderedStops()
    {
        var visitStart = new DateTimeOffset(2026, 9, 22, 9, 0, 0, TimeSpan.Zero);
        var generatedAt = visitStart.AddHours(-1);
        var service = new ItineraryOptimizationService(
            new FakeItineraryCandidateReader(
                [
                    new ItineraryCandidate(1, "Must Do", true, 20, true, 60),
                    new ItineraryCandidate(2, "Longer Wait", true, 10, true, 20),
                    new ItineraryCandidate(3, "Shorter Wait", true, 15, true, 5)
                ]),
            new FixedWalkingTimeService(5),
            new FixedTimeProvider(generatedAt));
        var command = new GenerateItineraryCommand(
            visitStart,
            visitStart.AddHours(8),
            null,
            [
                new ItineraryPreference(2, AttractionPreferenceLevel.WouldLike),
                new ItineraryPreference(1, AttractionPreferenceLevel.MustDo),
                new ItineraryPreference(3, AttractionPreferenceLevel.WouldLike)
            ]);

        var result = await service.GenerateAsync(10, command, CancellationToken.None);

        Assert.Equal([1L, 3L, 2L], result.Stops.Select(stop => stop.AttractionId));
        Assert.Equal([1, 2, 3], result.Stops.Select(stop => stop.Sequence));
        Assert.Equal(0, result.Stops[0].WalkingMinutes);
        Assert.Equal(5, result.Stops[1].WalkingMinutes);
        Assert.Equal(5, result.Stops[2].WalkingMinutes);
        Assert.Equal(10, result.TotalWalkingMinutes);
        Assert.Equal(85, result.TotalQueueMinutes);
        Assert.Equal(45, result.TotalAttractionMinutes);
        Assert.Equal(generatedAt, result.GeneratedAt);
        Assert.Equal("priority-live-history-walking-greedy-v2", result.AlgorithmVersion);
        Assert.Empty(result.UnscheduledAttractions);
    }

    [Fact]
    public async Task ItineraryOptimizer_UsesHistoricalWaitPatternsWithCurrentWaits()
    {
        var visitStart = new DateTimeOffset(2026, 9, 22, 9, 0, 0, TimeSpan.Zero);
        var generatedAt = visitStart.AddHours(-1);
        var candidateReader = new FakeItineraryCandidateReader(
            [
                new ItineraryCandidate(1, "Currently Short", true, 10, true, 5, 95),
                new ItineraryCandidate(2, "Consistently Moderate", true, 10, true, 20, 20),
                new ItineraryCandidate(3, "Historical Only", true, 10, true, null, 25)
            ]);
        var service = new ItineraryOptimizationService(
            candidateReader,
            new FixedWalkingTimeService(0),
            new FixedTimeProvider(generatedAt));
        var command = new GenerateItineraryCommand(
            visitStart,
            visitStart.AddHours(2),
            null,
            [
                new ItineraryPreference(1, AttractionPreferenceLevel.WouldLike),
                new ItineraryPreference(2, AttractionPreferenceLevel.WouldLike),
                new ItineraryPreference(3, AttractionPreferenceLevel.WouldLike)
            ]);

        var result = await service.GenerateAsync(1, command, CancellationToken.None);

        Assert.Equal([2L, 3L, 1L], result.Stops.Select(stop => stop.AttractionId));
        Assert.Equal([20, 25, 35], result.Stops.Select(stop => stop.QueueMinutes));
        Assert.Equal(visitStart, candidateReader.HistoricalTargetAt);
        Assert.Equal(generatedAt.AddMonths(-3), candidateReader.HistoricalWindowStart);
        Assert.Equal(generatedAt, candidateReader.HistoricalWindowEnd);
    }

    [Fact]
    public async Task ItineraryOptimizer_ChoosesEfficientWalkingRouteWithinPriority()
    {
        var visitStart = new DateTimeOffset(2026, 9, 22, 9, 0, 0, TimeSpan.Zero);
        var walkingTimeService = new ConfigurableWalkingTimeService(
            new Dictionary<(long, long), int>
            {
                [(99, 1)] = 20,
                [(99, 2)] = 1,
                [(2, 1)] = 3
            });
        var service = new ItineraryOptimizationService(
            new FakeItineraryCandidateReader(
                [
                    new ItineraryCandidate(1, "Nearby Wait", true, 10, true, 5),
                    new ItineraryCandidate(2, "Short Walk", true, 10, true, 10)
                ]),
            walkingTimeService,
            new FixedTimeProvider(visitStart.AddHours(-1)));
        var command = new GenerateItineraryCommand(
            visitStart,
            visitStart.AddHours(2),
            99,
            [
                new ItineraryPreference(1, AttractionPreferenceLevel.MustDo),
                new ItineraryPreference(2, AttractionPreferenceLevel.MustDo)
            ]);

        var result = await service.GenerateAsync(1, command, CancellationToken.None);

        Assert.Equal([2L, 1L], result.Stops.Select(stop => stop.AttractionId));
        Assert.Equal([1, 3], result.Stops.Select(stop => stop.WalkingMinutes));
    }

    [Fact]
    public async Task ItineraryOptimizer_ReportsWhyAttractionsWereNotScheduled()
    {
        var visitStart = new DateTimeOffset(2026, 9, 22, 9, 0, 0, TimeSpan.Zero);
        var service = new ItineraryOptimizationService(
            new FakeItineraryCandidateReader(
                [
                    new ItineraryCandidate(1, "Skipped", true, 10, true, 0),
                    new ItineraryCandidate(2, "Inactive", false, 10, true, 0),
                    new ItineraryCandidate(3, "Closed", true, 10, false, null),
                    new ItineraryCandidate(5, "Too Long", true, 60, true, 0)
                ]),
            new FixedWalkingTimeService(0),
            new FixedTimeProvider(visitStart));
        var command = new GenerateItineraryCommand(
            visitStart,
            visitStart.AddMinutes(30),
            null,
            [
                new ItineraryPreference(1, AttractionPreferenceLevel.Skip),
                new ItineraryPreference(2, AttractionPreferenceLevel.WouldLike),
                new ItineraryPreference(3, AttractionPreferenceLevel.MustDo),
                new ItineraryPreference(4, AttractionPreferenceLevel.WouldLike),
                new ItineraryPreference(5, AttractionPreferenceLevel.MustDo)
            ]);

        var result = await service.GenerateAsync(1, command, CancellationToken.None);

        Assert.Empty(result.Stops);
        Assert.Contains(
            result.UnscheduledAttractions,
            item => item.AttractionId == 1 &&
                item.Reason == UnscheduledAttractionReason.SkippedByVisitor);
        Assert.Contains(
            result.UnscheduledAttractions,
            item => item.AttractionId == 2 &&
                item.Reason == UnscheduledAttractionReason.AttractionUnavailable);
        Assert.Contains(
            result.UnscheduledAttractions,
            item => item.AttractionId == 3 &&
                item.Reason == UnscheduledAttractionReason.AttractionClosed);
        Assert.Contains(
            result.UnscheduledAttractions,
            item => item.AttractionId == 4 &&
                item.Reason == UnscheduledAttractionReason.AttractionUnavailable);
        Assert.Contains(
            result.UnscheduledAttractions,
            item => item.AttractionId == 5 &&
                item.Reason == UnscheduledAttractionReason.VisitWindowExceeded);
    }

    [Fact]
    public async Task ItineraryOptimizer_UsesDefaultDurationAndStartingLocation()
    {
        var visitStart = new DateTimeOffset(2026, 9, 22, 9, 0, 0, TimeSpan.Zero);
        var walkingTimeService = new FixedWalkingTimeService(7);
        var service = new ItineraryOptimizationService(
            new FakeItineraryCandidateReader(
                [new ItineraryCandidate(1, "Attraction", true, null, null, null)]),
            walkingTimeService,
            new FixedTimeProvider(visitStart));
        var command = new GenerateItineraryCommand(
            visitStart,
            visitStart.AddHours(1),
            99,
            [new ItineraryPreference(1, AttractionPreferenceLevel.WouldLike)]);

        var result = await service.GenerateAsync(1, command, CancellationToken.None);

        var stop = Assert.Single(result.Stops);
        Assert.Equal(7, stop.WalkingMinutes);
        Assert.Equal(0, stop.QueueMinutes);
        Assert.Equal(10, stop.AttractionDurationMinutes);
        Assert.Equal((99L, 1L), Assert.Single(walkingTimeService.Requests));
    }

    [Fact]
    public async Task ItineraryOptimizer_DoesNotScheduleWithoutAWalkingRoute()
    {
        var visitStart = new DateTimeOffset(2026, 9, 22, 9, 0, 0, TimeSpan.Zero);
        var service = new ItineraryOptimizationService(
            new FakeItineraryCandidateReader(
                [new ItineraryCandidate(1, "Attraction", true, 10, true, 0)]),
            new UnavailableWalkingTimeService(),
            new FixedTimeProvider(visitStart));
        var command = new GenerateItineraryCommand(
            visitStart,
            visitStart.AddHours(1),
            99,
            [new ItineraryPreference(1, AttractionPreferenceLevel.MustDo)]);

        var result = await service.GenerateAsync(1, command, CancellationToken.None);

        Assert.Empty(result.Stops);
        var unscheduled = Assert.Single(result.UnscheduledAttractions);
        Assert.Equal(
            UnscheduledAttractionReason.WalkingRouteUnavailable,
            unscheduled.Reason);
    }

    [Fact]
    public async Task ItineraryOptimizer_RejectsInvalidRequests()
    {
        var now = DateTimeOffset.UtcNow;
        var service = new ItineraryOptimizationService(
            new FakeItineraryCandidateReader([]),
            new FixedWalkingTimeService(0),
            new FixedTimeProvider(now));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.GenerateAsync(
                0,
                ValidItineraryCommand(now),
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GenerateAsync(
                1,
                ValidItineraryCommand(now) with { VisitEndAt = now },
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.GenerateAsync(
                1,
                ValidItineraryCommand(now) with
                {
                    VisitEndAt = now.Add(
                        ItineraryOptimizationService.MaximumVisitWindow).AddMinutes(1)
                },
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GenerateAsync(
                1,
                ValidItineraryCommand(now) with { Preferences = [] },
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GenerateAsync(
                1,
                ValidItineraryCommand(now) with
                {
                    Preferences =
                    [
                        new ItineraryPreference(1, AttractionPreferenceLevel.MustDo),
                        new ItineraryPreference(1, AttractionPreferenceLevel.Skip)
                    ]
                },
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.GenerateAsync(
                1,
                ValidItineraryCommand(now) with
                {
                    Preferences =
                    [
                        new ItineraryPreference(
                            1,
                            (AttractionPreferenceLevel)999)
                    ]
                },
                CancellationToken.None));
    }

    private static GenerateItineraryCommand ValidItineraryCommand(DateTimeOffset start) =>
        new(
            start,
            start.AddHours(8),
            null,
            [new ItineraryPreference(1, AttractionPreferenceLevel.MustDo)]);

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

    private sealed class FakeItineraryCandidateReader(
        IReadOnlyList<ItineraryCandidate> candidates) : IItineraryCandidateReader
    {
        public DateTimeOffset? HistoricalTargetAt { get; private set; }
        public DateTimeOffset? HistoricalWindowStart { get; private set; }
        public DateTimeOffset? HistoricalWindowEnd { get; private set; }

        public Task<IReadOnlyList<ItineraryCandidate>> GetCandidatesAsync(
            long parkId,
            IReadOnlyCollection<long> attractionIds,
            DateTimeOffset historicalTargetAt,
            DateTimeOffset historicalWindowStart,
            DateTimeOffset historicalWindowEnd,
            CancellationToken cancellationToken)
        {
            HistoricalTargetAt = historicalTargetAt;
            HistoricalWindowStart = historicalWindowStart;
            HistoricalWindowEnd = historicalWindowEnd;
            return Task.FromResult(candidates);
        }
    }

    private sealed class FixedWalkingTimeService(int walkingMinutes)
        : IWalkingTimeService
    {
        public List<(long FromAttractionId, long ToAttractionId)> Requests { get; } = [];

        public Task<WalkingTimeEstimateResult?> EstimateAsync(
            long parkId,
            long fromAttractionId,
            long toAttractionId,
            CancellationToken cancellationToken)
        {
            Requests.Add((fromAttractionId, toAttractionId));
            return Task.FromResult<WalkingTimeEstimateResult?>(new(
                parkId,
                fromAttractionId,
                "Origin",
                toAttractionId,
                "Destination",
                WalkingTimeEstimateStatus.Available,
                null,
                walkingMinutes * 84,
                walkingMinutes,
                WalkingRouteSource.ParkGraph,
                null,
                1m,
                1.4m,
                "test"));
        }
    }

    private sealed class UnavailableWalkingTimeService : IWalkingTimeService
    {
        public Task<WalkingTimeEstimateResult?> EstimateAsync(
            long parkId,
            long fromAttractionId,
            long toAttractionId,
            CancellationToken cancellationToken) =>
            Task.FromResult<WalkingTimeEstimateResult?>(null);
    }

    private sealed class ConfigurableWalkingTimeService(
        IReadOnlyDictionary<(long FromAttractionId, long ToAttractionId), int> walkingMinutes)
        : IWalkingTimeService
    {
        public Task<WalkingTimeEstimateResult?> EstimateAsync(
            long parkId,
            long fromAttractionId,
            long toAttractionId,
            CancellationToken cancellationToken)
        {
            var minutes = walkingMinutes[(fromAttractionId, toAttractionId)];
            return Task.FromResult<WalkingTimeEstimateResult?>(new(
                parkId,
                fromAttractionId,
                "Origin",
                toAttractionId,
                "Destination",
                WalkingTimeEstimateStatus.Available,
                null,
                minutes * 84,
                minutes,
                WalkingRouteSource.ParkGraph,
                null,
                1m,
                1.4m,
                "test"));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
