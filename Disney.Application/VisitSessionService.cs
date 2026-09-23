namespace Disney.Application;

public sealed class VisitSessionService(
    IItineraryOptimizationService optimizationService,
    IVisitSessionStore store,
    TimeProvider timeProvider) : IVisitSessionService
{
    public const int MinimumPartySize = 1;
    public const int MaximumPartySize = 20;
    public const int MaterialQueueChangeMinutes = 15;

    public async Task<VisitSession> StartAsync(
        long parkId,
        StartVisitSessionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.PartySize is < MinimumPartySize or > MaximumPartySize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(command.PartySize),
                $"Party size must be between {MinimumPartySize} and {MaximumPartySize}.");
        }

        var itinerary = await optimizationService.GenerateAsync(
            parkId,
            command.Itinerary,
            cancellationToken);
        if (itinerary.Stops.Count == 0)
        {
            throw new ArgumentException(
                "A visit session requires at least one scheduled attraction.",
                nameof(command.Itinerary));
        }

        var startedAt = timeProvider.GetUtcNow();
        var session = new VisitSession(
            Guid.NewGuid(),
            parkId,
            itinerary.VisitStartAt,
            itinerary.VisitEndAt,
            command.PartySize,
            startedAt,
            startedAt,
            VisitSessionStatus.Active,
            itinerary.Stops.Select(stop => new VisitSessionStop(
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
                VisitSessionStopStatus.Pending,
                null)).ToArray(),
            itinerary.TotalWalkingMinutes,
            itinerary.TotalQueueMinutes,
            itinerary.TotalAttractionMinutes,
            itinerary.AlgorithmVersion);

        await store.CreateAsync(session, cancellationToken);
        return session;
    }

    public Task<VisitSession?> GetAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        ValidateSessionId(sessionId);
        return store.GetAsync(sessionId, cancellationToken);
    }

    public Task<VisitSession?> CompleteAttractionAsync(
        Guid sessionId,
        long attractionId,
        CancellationToken cancellationToken) =>
        SetStopStatusAsync(
            sessionId,
            attractionId,
            VisitSessionStopStatus.Completed,
            cancellationToken);

    public Task<VisitSession?> SkipAttractionAsync(
        Guid sessionId,
        long attractionId,
        CancellationToken cancellationToken) =>
        SetStopStatusAsync(
            sessionId,
            attractionId,
            VisitSessionStopStatus.Skipped,
            cancellationToken);

    public async Task<VisitSession?> ReplanForCurrentConditionsAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        ValidateSessionId(sessionId);
        var session = await store.GetAsync(sessionId, cancellationToken);
        return session is null
            ? null
            : await ReplanRemainingAsync(session, false, cancellationToken);
    }

    private async Task<VisitSession?> SetStopStatusAsync(
        Guid sessionId,
        long attractionId,
        VisitSessionStopStatus status,
        CancellationToken cancellationToken)
    {
        ValidateSessionId(sessionId);
        if (attractionId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attractionId),
                "Attraction ID must be greater than zero.");
        }

        var session = await store.GetAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return null;
        }

        var stop = session.Stops.SingleOrDefault(
            candidate => candidate.AttractionId == attractionId);
        if (stop is null)
        {
            return null;
        }

        if (stop.Status == status)
        {
            return session;
        }

        if (stop.Status != VisitSessionStopStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Attraction {attractionId} is already {stop.Status.ToString().ToLowerInvariant()}.");
        }

        var changed = await store.TrySetStopStatusAsync(
            sessionId,
            attractionId,
            status,
            timeProvider.GetUtcNow(),
            cancellationToken);
        var updated = await store.GetAsync(sessionId, cancellationToken);
        if (changed || updated?.Stops.Single(
                candidate => candidate.AttractionId == attractionId).Status == status)
        {
            return updated is null
                ? null
                : await ReplanRemainingAsync(updated, true, cancellationToken);
        }

        throw new InvalidOperationException(
            "Visit progress changed in another request. Reload the visit and try again.");
    }

    private async Task<VisitSession> ReplanRemainingAsync(
        VisitSession session,
        bool force,
        CancellationToken cancellationToken)
    {
        var pendingStops = session.Stops
            .Where(stop => stop.Status == VisitSessionStopStatus.Pending)
            .ToArray();
        if (pendingStops.Length == 0 || session.Status == VisitSessionStatus.Completed)
        {
            return session;
        }

        var now = timeProvider.GetUtcNow();
        var visitStartAt = now > session.VisitStartAt ? now : session.VisitStartAt;
        if (visitStartAt >= session.VisitEndAt)
        {
            return session;
        }

        var lastCompletedAttractionId = session.Stops
            .Where(stop => stop.Status == VisitSessionStopStatus.Completed)
            .OrderByDescending(stop => stop.StatusChangedAt)
            .ThenByDescending(stop => stop.Sequence)
            .Select(stop => (long?)stop.AttractionId)
            .FirstOrDefault();
        var itinerary = await optimizationService.GenerateAsync(
            session.ParkId,
            new GenerateItineraryCommand(
                visitStartAt,
                session.VisitEndAt,
                lastCompletedAttractionId,
                pendingStops
                    .Select(stop => new ItineraryPreference(
                        stop.AttractionId,
                        stop.Preference))
                    .ToArray()),
            cancellationToken);

        if (!force && !HasMaterialConditionsChange(pendingStops, itinerary))
        {
            return session;
        }

        var resolvedStops = session.Stops
            .Where(stop => stop.Status != VisitSessionStopStatus.Pending)
            .ToArray();
        var nextSequence = resolvedStops
            .Select(stop => stop.Sequence)
            .DefaultIfEmpty(0)
            .Max();
        var replacementStops = itinerary.Stops
            .Select((stop, index) => new VisitSessionStop(
                nextSequence + index + 1,
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
                VisitSessionStopStatus.Pending,
                null))
            .ToArray();
        var retainedStops = resolvedStops
            .Where(stop => stop.Status == VisitSessionStopStatus.Completed)
            .Concat(replacementStops)
            .ToArray();
        var changed = await store.TryReplacePendingStopsAsync(
            session.Id,
            session.UpdatedAt,
            replacementStops,
            retainedStops.Sum(stop => stop.WalkingMinutes),
            retainedStops.Sum(stop => stop.QueueMinutes),
            retainedStops.Sum(stop => stop.AttractionDurationMinutes),
            itinerary.AlgorithmVersion,
            now,
            cancellationToken);
        if (!changed)
        {
            return await store.GetAsync(session.Id, cancellationToken)
                ?? throw new InvalidOperationException(
                    "The visit session was removed while it was being replanned.");
        }

        return await store.GetAsync(session.Id, cancellationToken)
            ?? throw new InvalidOperationException(
                "The replanned visit session could not be reloaded.");
    }

    private static bool HasMaterialConditionsChange(
        IReadOnlyList<VisitSessionStop> pendingStops,
        OptimizedItineraryResult itinerary)
    {
        var replannedByAttraction = itinerary.Stops.ToDictionary(
            stop => stop.AttractionId);
        if (pendingStops.Any(stop => !replannedByAttraction.ContainsKey(stop.AttractionId)))
        {
            return true;
        }

        return pendingStops.Any(stop =>
            Math.Abs(
                replannedByAttraction[stop.AttractionId].QueueMinutes -
                stop.QueueMinutes) >= MaterialQueueChangeMinutes);
    }

    private static void ValidateSessionId(Guid sessionId)
    {
        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Visit session ID cannot be empty.",
                nameof(sessionId));
        }
    }
}
