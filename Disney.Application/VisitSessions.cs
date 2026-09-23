namespace Disney.Application;

public enum VisitSessionStatus
{
    Active,
    Completed
}

public enum VisitSessionStopStatus
{
    Pending,
    Completed,
    Skipped
}

public sealed record VisitSessionStop(
    int Sequence,
    long AttractionId,
    string AttractionName,
    AttractionPreferenceLevel Preference,
    DateTimeOffset TravelStartsAt,
    int WalkingMinutes,
    DateTimeOffset QueueStartsAt,
    int QueueMinutes,
    DateTimeOffset AttractionStartsAt,
    int AttractionDurationMinutes,
    DateTimeOffset CompletesAt,
    VisitSessionStopStatus Status,
    DateTimeOffset? StatusChangedAt)
{
    public static VisitSessionStop CreatePending(ItineraryStop stop, int sequence) =>
        new(
            sequence,
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
            null);
}

public sealed record VisitSession(
    Guid Id,
    long ParkId,
    DateTimeOffset VisitStartAt,
    DateTimeOffset VisitEndAt,
    int PartySize,
    DateTimeOffset StartedAt,
    DateTimeOffset UpdatedAt,
    VisitSessionStatus Status,
    IReadOnlyList<VisitSessionStop> Stops,
    int TotalWalkingMinutes,
    int TotalQueueMinutes,
    int TotalAttractionMinutes,
    string AlgorithmVersion);

public sealed record StartVisitSessionCommand(
    int PartySize,
    GenerateItineraryCommand Itinerary);

public interface IVisitSessionStore
{
    Task CreateAsync(VisitSession session, CancellationToken cancellationToken);

    Task<VisitSession?> GetAsync(
        Guid sessionId,
        CancellationToken cancellationToken);

    Task<bool> TrySetStopStatusAsync(
        Guid sessionId,
        long attractionId,
        VisitSessionStopStatus status,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken);

    Task<bool> TryReplacePendingStopsAsync(
        Guid sessionId,
        DateTimeOffset expectedUpdatedAt,
        IReadOnlyList<VisitSessionStop> pendingStops,
        int totalWalkingMinutes,
        int totalQueueMinutes,
        int totalAttractionMinutes,
        string algorithmVersion,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken);
}

public interface IVisitSessionService
{
    Task<VisitSession> StartAsync(
        long parkId,
        StartVisitSessionCommand command,
        CancellationToken cancellationToken);

    Task<VisitSession?> GetAsync(
        Guid sessionId,
        CancellationToken cancellationToken);

    Task<VisitSession?> CompleteAttractionAsync(
        Guid sessionId,
        long attractionId,
        CancellationToken cancellationToken);

    Task<VisitSession?> SkipAttractionAsync(
        Guid sessionId,
        long attractionId,
        CancellationToken cancellationToken);

    Task<VisitSession?> ReplanForCurrentConditionsAsync(
        Guid sessionId,
        CancellationToken cancellationToken);
}
