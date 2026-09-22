namespace Disney.Application;

public sealed class VisitSessionService(
    IItineraryOptimizationService optimizationService,
    IVisitSessionStore store,
    TimeProvider timeProvider) : IVisitSessionService
{
    public const int MinimumPartySize = 1;
    public const int MaximumPartySize = 20;

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
            return updated;
        }

        throw new InvalidOperationException(
            "Visit progress changed in another request. Reload the visit and try again.");
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
