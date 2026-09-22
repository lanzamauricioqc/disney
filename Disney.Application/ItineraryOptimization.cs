namespace Disney.Application;

public enum AttractionPreferenceLevel
{
    MustDo,
    WouldLike,
    Skip
}

public enum UnscheduledAttractionReason
{
    SkippedByVisitor,
    AttractionUnavailable,
    AttractionClosed,
    WalkingRouteUnavailable,
    VisitWindowExceeded
}

public sealed record ItineraryPreference(
    long AttractionId,
    AttractionPreferenceLevel Level);

public sealed record GenerateItineraryCommand(
    DateTimeOffset VisitStartAt,
    DateTimeOffset VisitEndAt,
    long? StartingAttractionId,
    IReadOnlyList<ItineraryPreference> Preferences);

public sealed record ItineraryCandidate(
    long AttractionId,
    string AttractionName,
    bool IsActive,
    int? DurationMinutes,
    bool? IsOpen,
    short? WaitMinutes);

public sealed record ItineraryStop(
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
    DateTimeOffset CompletesAt);

public sealed record UnscheduledAttraction(
    long AttractionId,
    AttractionPreferenceLevel Preference,
    UnscheduledAttractionReason Reason);

public sealed record OptimizedItineraryResult(
    long ParkId,
    DateTimeOffset VisitStartAt,
    DateTimeOffset VisitEndAt,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ItineraryStop> Stops,
    IReadOnlyList<UnscheduledAttraction> UnscheduledAttractions,
    int TotalWalkingMinutes,
    int TotalQueueMinutes,
    int TotalAttractionMinutes,
    string AlgorithmVersion);

public interface IItineraryCandidateReader
{
    Task<IReadOnlyList<ItineraryCandidate>> GetCandidatesAsync(
        long parkId,
        IReadOnlyCollection<long> attractionIds,
        CancellationToken cancellationToken);
}

public interface IItineraryOptimizationService
{
    Task<OptimizedItineraryResult> GenerateAsync(
        long parkId,
        GenerateItineraryCommand command,
        CancellationToken cancellationToken);
}
