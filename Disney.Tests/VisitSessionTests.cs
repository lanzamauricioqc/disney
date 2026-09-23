using Disney.Application;

namespace Disney.Tests;

public sealed class VisitSessionTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 22, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task StartVisitSession_OptimizesAndPersistsInitialProgress()
    {
        var store = new FakeVisitSessionStore();
        var optimizationService = new FakeOptimizationService(CreateItinerary());
        var service = CreateService(store, optimizationService);
        var command = new StartVisitSessionCommand(4, CreateItineraryCommand());

        var session = await service.StartAsync(10, command, CancellationToken.None);

        Assert.Equal(10, optimizationService.ParkId);
        Assert.Same(command.Itinerary, optimizationService.Command);
        Assert.Same(session, store.Session);
        Assert.Equal(4, session.PartySize);
        Assert.Equal(Now, session.StartedAt);
        Assert.Equal(VisitSessionStatus.Active, session.Status);
        Assert.All(
            session.Stops,
            stop => Assert.Equal(VisitSessionStopStatus.Pending, stop.Status));
    }

    [Fact]
    public async Task StartVisitSession_RejectsInvalidInput()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.StartAsync(1, null!, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.StartAsync(
                1,
                new StartVisitSessionCommand(0, CreateItineraryCommand()),
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.StartAsync(
                1,
                new StartVisitSessionCommand(21, CreateItineraryCommand()),
                CancellationToken.None));
    }

    [Fact]
    public async Task StartVisitSession_RequiresScheduledAttraction()
    {
        var itinerary = CreateItinerary() with { Stops = [] };
        var service = CreateService(
            optimizationService: new FakeOptimizationService(itinerary));

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.StartAsync(
                1,
                new StartVisitSessionCommand(2, CreateItineraryCommand()),
                CancellationToken.None));
    }

    [Fact]
    public async Task GetVisitSession_ReturnsPersistedSession()
    {
        var session = CreateSession();
        var service = CreateService(new FakeVisitSessionStore { Session = session });

        var result = await service.GetAsync(session.Id, CancellationToken.None);

        Assert.Same(session, result);
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetAsync(Guid.Empty, CancellationToken.None));
    }

    [Fact]
    public async Task CompleteAttraction_PersistsProgressAndCompletesFinalStop()
    {
        var session = CreateSession(stopCount: 1);
        var store = new FakeVisitSessionStore { Session = session };
        var service = CreateService(store);

        var result = await service.CompleteAttractionAsync(
            session.Id,
            101,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(VisitSessionStatus.Completed, result.Status);
        Assert.Equal(VisitSessionStopStatus.Completed, result.Stops[0].Status);
        Assert.Equal(Now, result.Stops[0].StatusChangedAt);
        Assert.Equal(0, store.ReplaceCount);
    }

    [Fact]
    public async Task SkipAttraction_PersistsSkippedProgress()
    {
        var session = CreateSession();
        var store = new FakeVisitSessionStore { Session = session };
        var service = CreateService(store);

        var result = await service.SkipAttractionAsync(
            session.Id,
            101,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(VisitSessionStatus.Active, result.Status);
        Assert.Equal(VisitSessionStopStatus.Skipped, result.Stops[0].Status);
        Assert.Equal(1, store.ReplaceCount);
        Assert.Equal(102, result.Stops.Single(
            stop => stop.Status == VisitSessionStopStatus.Pending).AttractionId);
    }

    [Fact]
    public async Task CompleteAttraction_ReplansRemainingStopsFromCurrentLocation()
    {
        var session = CreateSession();
        var store = new FakeVisitSessionStore { Session = session };
        var optimizationService = new FakeOptimizationService(
            CreateItinerary(),
            filterToRequestedAttractions: true);
        var service = CreateService(store, optimizationService);

        var result = await service.CompleteAttractionAsync(
            session.Id,
            101,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(1, store.ReplaceCount);
        Assert.Equal(101, optimizationService.Command!.StartingAttractionId);
        Assert.Equal(session.VisitStartAt, optimizationService.Command.VisitStartAt);
        Assert.Equal(
            [102L],
            optimizationService.Command.Preferences.Select(
                preference => preference.AttractionId));
        Assert.Equal(
            [101L, 102L],
            result.Stops.Select(stop => stop.AttractionId));
    }

    [Fact]
    public async Task CurrentConditions_ReplansWhenAttractionCloses()
    {
        var session = CreateSession();
        var store = new FakeVisitSessionStore { Session = session };
        var replanned = CreateItinerary() with
        {
            Stops = [CreateItinerary().Stops[1]],
            UnscheduledAttractions =
            [
                new UnscheduledAttraction(
                    101,
                    AttractionPreferenceLevel.MustDo,
                    UnscheduledAttractionReason.AttractionClosed)
            ]
        };
        var service = CreateService(
            store,
            new FakeOptimizationService(replanned));

        var result = await service.ReplanForCurrentConditionsAsync(
            session.Id,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(1, store.ReplaceCount);
        Assert.Equal([102L], result.Stops.Select(stop => stop.AttractionId));
    }

    [Fact]
    public async Task CurrentConditions_ReplansAfterMaterialQueueChange()
    {
        var session = CreateSession();
        var store = new FakeVisitSessionStore { Session = session };
        var itinerary = CreateItinerary();
        var changedStops = itinerary.Stops
            .Select(stop => stop.AttractionId == 101
                ? stop with
                {
                    QueueMinutes =
                        stop.QueueMinutes +
                        VisitSessionService.MaterialQueueChangeMinutes
                }
                : stop)
            .Reverse()
            .Select((stop, index) => stop with { Sequence = index + 1 })
            .ToArray();
        var service = CreateService(
            store,
            new FakeOptimizationService(itinerary with { Stops = changedStops }));

        var result = await service.ReplanForCurrentConditionsAsync(
            session.Id,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(1, store.ReplaceCount);
        Assert.Equal([102L, 101L], result.Stops.Select(stop => stop.AttractionId));
    }

    [Fact]
    public async Task CurrentConditions_PreservesStablePlanAfterMinorQueueChange()
    {
        var session = CreateSession();
        var store = new FakeVisitSessionStore { Session = session };
        var itinerary = CreateItinerary();
        var changedStops = itinerary.Stops
            .Select(stop => stop with
            {
                QueueMinutes =
                    stop.QueueMinutes +
                    VisitSessionService.MaterialQueueChangeMinutes -
                    1
            })
            .ToArray();
        var service = CreateService(
            store,
            new FakeOptimizationService(itinerary with { Stops = changedStops }));

        var result = await service.ReplanForCurrentConditionsAsync(
            session.Id,
            CancellationToken.None);

        Assert.Same(session, result);
        Assert.Equal(0, store.ReplaceCount);
    }

    [Fact]
    public async Task UpdateAttraction_IsIdempotentForSameStatus()
    {
        var session = CreateSession(
            VisitSessionStopStatus.Completed,
            Now.AddMinutes(-1));
        var store = new FakeVisitSessionStore { Session = session };
        var service = CreateService(store);

        var result = await service.CompleteAttractionAsync(
            session.Id,
            101,
            CancellationToken.None);

        Assert.Same(session, result);
        Assert.Equal(0, store.UpdateCount);
    }

    [Fact]
    public async Task UpdateAttraction_RejectsConflictingStatus()
    {
        var session = CreateSession(
            VisitSessionStopStatus.Completed,
            Now.AddMinutes(-1));
        var service = CreateService(new FakeVisitSessionStore { Session = session });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SkipAttractionAsync(
                session.Id,
                101,
                CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAttraction_ReturnsNullWhenSessionOrStopDoesNotExist()
    {
        var service = CreateService();
        Assert.Null(await service.CompleteAttractionAsync(
            Guid.NewGuid(),
            101,
            CancellationToken.None));

        var session = CreateSession();
        service = CreateService(new FakeVisitSessionStore { Session = session });
        Assert.Null(await service.CompleteAttractionAsync(
            session.Id,
            999,
            CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAttraction_ValidatesIdentifiers()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CompleteAttractionAsync(
                Guid.Empty,
                1,
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.CompleteAttractionAsync(
                Guid.NewGuid(),
                0,
                CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAttraction_HandlesConcurrentSameStatusRetry()
    {
        var session = CreateSession();
        var store = new FakeVisitSessionStore
        {
            Session = session,
            SimulatedConcurrentStatus = VisitSessionStopStatus.Completed
        };
        var service = CreateService(store);

        var result = await service.CompleteAttractionAsync(
            session.Id,
            101,
            CancellationToken.None);

        Assert.Equal(VisitSessionStopStatus.Completed, result!.Stops[0].Status);
    }

    [Fact]
    public async Task UpdateAttraction_RejectsConcurrentConflictingUpdate()
    {
        var session = CreateSession();
        var store = new FakeVisitSessionStore
        {
            Session = session,
            SimulatedConcurrentStatus = VisitSessionStopStatus.Skipped
        };
        var service = CreateService(store);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CompleteAttractionAsync(
                session.Id,
                101,
                CancellationToken.None));
    }

    private static VisitSessionService CreateService(
        FakeVisitSessionStore? store = null,
        FakeOptimizationService? optimizationService = null) =>
        new(
            optimizationService ?? new FakeOptimizationService(
                CreateItinerary(),
                filterToRequestedAttractions: true),
            store ?? new FakeVisitSessionStore(),
            new FixedTimeProvider(Now));

    private static VisitSession CreateSession(
        VisitSessionStopStatus firstStatus = VisitSessionStopStatus.Pending,
        DateTimeOffset? statusChangedAt = null,
        int stopCount = 2)
    {
        var itinerary = CreateItinerary();
        var stops = itinerary.Stops.Take(stopCount).Select((stop, index) =>
            new VisitSessionStop(
                stop.Sequence,
                stop.AttractionId,
                stop.AttractionName,
                stop.Preference,
                stop.TravelStartsAt,
                stop.WalkingMinutes,
                stop.QueueStartsAt,
                stop.QueueMinutes,
                stop.AttractionStartsAt,
                stop.AttractionDurationMinutes,
                stop.CompletesAt,
                index == 0 ? firstStatus : VisitSessionStopStatus.Pending,
                index == 0 ? statusChangedAt : null)).ToArray();
        return new VisitSession(
            Guid.NewGuid(),
            itinerary.ParkId,
            itinerary.VisitStartAt,
            itinerary.VisitEndAt,
            2,
            Now.AddMinutes(-5),
            Now.AddMinutes(-5),
            VisitSessionStatus.Active,
            stops,
            itinerary.TotalWalkingMinutes,
            itinerary.TotalQueueMinutes,
            itinerary.TotalAttractionMinutes,
            itinerary.AlgorithmVersion);
    }

    private static OptimizedItineraryResult CreateItinerary()
    {
        var firstStart = Now.AddHours(1);
        var secondStart = firstStart.AddHours(1);
        return new OptimizedItineraryResult(
            10,
            firstStart,
            firstStart.AddHours(8),
            Now.AddMinutes(-10),
            [
                CreateStop(1, 101, "First attraction", firstStart),
                CreateStop(2, 102, "Second attraction", secondStart)
            ],
            [],
            10,
            30,
            20,
            "test-algorithm");
    }

    private static ItineraryStop CreateStop(
        int sequence,
        long attractionId,
        string name,
        DateTimeOffset startsAt) =>
        new(
            sequence,
            attractionId,
            name,
            AttractionPreferenceLevel.MustDo,
            startsAt,
            5,
            startsAt.AddMinutes(5),
            15,
            startsAt.AddMinutes(20),
            10,
            startsAt.AddMinutes(30));

    private static GenerateItineraryCommand CreateItineraryCommand() =>
        new(
            Now.AddHours(1),
            Now.AddHours(9),
            null,
            [new ItineraryPreference(101, AttractionPreferenceLevel.MustDo)]);

    private sealed class FakeOptimizationService(
        OptimizedItineraryResult result,
        bool filterToRequestedAttractions = false)
        : IItineraryOptimizationService
    {
        public long ParkId { get; private set; }
        public GenerateItineraryCommand? Command { get; private set; }

        public Task<OptimizedItineraryResult> GenerateAsync(
            long parkId,
            GenerateItineraryCommand command,
            CancellationToken cancellationToken)
        {
            ParkId = parkId;
            Command = command;
            if (!filterToRequestedAttractions)
            {
                return Task.FromResult(result);
            }

            var requestedIds = command.Preferences
                .Select(preference => preference.AttractionId)
                .ToHashSet();
            var stops = result.Stops
                .Where(stop => requestedIds.Contains(stop.AttractionId))
                .Select((stop, index) => stop with { Sequence = index + 1 })
                .ToArray();
            return Task.FromResult(result with
            {
                VisitStartAt = command.VisitStartAt,
                VisitEndAt = command.VisitEndAt,
                Stops = stops
            });
        }
    }

    private sealed class FakeVisitSessionStore : IVisitSessionStore
    {
        public VisitSession? Session { get; set; }
        public VisitSessionStopStatus? SimulatedConcurrentStatus { get; init; }
        public int UpdateCount { get; private set; }
        public int ReplaceCount { get; private set; }

        public Task CreateAsync(
            VisitSession session,
            CancellationToken cancellationToken)
        {
            Session = session;
            return Task.CompletedTask;
        }

        public Task<VisitSession?> GetAsync(
            Guid sessionId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Session?.Id == sessionId ? Session : null);

        public Task<bool> TrySetStopStatusAsync(
            Guid sessionId,
            long attractionId,
            VisitSessionStopStatus status,
            DateTimeOffset changedAt,
            CancellationToken cancellationToken)
        {
            UpdateCount++;
            var appliedStatus = SimulatedConcurrentStatus ?? status;
            var stop = Session!.Stops.Single(
                candidate => candidate.AttractionId == attractionId);
            var updatedStops = Session.Stops
                .Select(candidate => candidate.AttractionId == attractionId
                    ? candidate with
                    {
                        Status = appliedStatus,
                        StatusChangedAt = changedAt
                    }
                    : candidate)
                .ToArray();
            Session = Session with
            {
                UpdatedAt = changedAt,
                Status = updatedStops.All(
                    candidate => candidate.Status != VisitSessionStopStatus.Pending)
                        ? VisitSessionStatus.Completed
                        : VisitSessionStatus.Active,
                Stops = updatedStops
            };
            return Task.FromResult(SimulatedConcurrentStatus is null);
        }

        public Task<bool> TryReplacePendingStopsAsync(
            Guid sessionId,
            DateTimeOffset expectedUpdatedAt,
            IReadOnlyList<VisitSessionStop> pendingStops,
            int totalWalkingMinutes,
            int totalQueueMinutes,
            int totalAttractionMinutes,
            string algorithmVersion,
            DateTimeOffset changedAt,
            CancellationToken cancellationToken)
        {
            ReplaceCount++;
            if (Session?.Id != sessionId || Session.UpdatedAt != expectedUpdatedAt)
            {
                return Task.FromResult(false);
            }

            var resolvedStops = Session.Stops
                .Where(stop => stop.Status != VisitSessionStopStatus.Pending);
            var stops = resolvedStops.Concat(pendingStops)
                .OrderBy(stop => stop.Sequence)
                .ToArray();
            Session = Session with
            {
                UpdatedAt = changedAt,
                Status = pendingStops.Count == 0
                    ? VisitSessionStatus.Completed
                    : VisitSessionStatus.Active,
                Stops = stops,
                TotalWalkingMinutes = totalWalkingMinutes,
                TotalQueueMinutes = totalQueueMinutes,
                TotalAttractionMinutes = totalAttractionMinutes,
                AlgorithmVersion = algorithmVersion
            };
            return Task.FromResult(true);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
