namespace Disney.Application;

public sealed class ItineraryOptimizationService(
    IItineraryCandidateReader candidateReader,
    IWalkingTimeService walkingTimeService,
    TimeProvider timeProvider) : IItineraryOptimizationService
{
    private const int DefaultAttractionDurationMinutes = 10;
    private const string AlgorithmVersion = "priority-live-history-walking-greedy-v3";
    public static readonly TimeSpan MaximumVisitWindow = TimeSpan.FromDays(1);

    public async Task<OptimizedItineraryResult> GenerateAsync(
        long parkId,
        GenerateItineraryCommand command,
        CancellationToken cancellationToken)
    {
        Validate(parkId, command);
        var generatedAt = timeProvider.GetUtcNow();
        var preferencesByAttraction = command.Preferences.ToDictionary(
            preference => preference.AttractionId);
        var candidates = await candidateReader.GetCandidatesAsync(
            parkId,
            preferencesByAttraction.Keys.ToArray(),
            generatedAt.Subtract(QueueDataFreshness.MaximumLiveObservationAge),
            generatedAt,
            command.VisitStartAt,
            generatedAt.AddMonths(-QueueHistoryWindow.LookbackMonths),
            generatedAt,
            cancellationToken);
        var candidatesById = candidates.ToDictionary(candidate => candidate.AttractionId);
        var unscheduled = CreateInitiallyUnscheduled(
            command.Preferences,
            candidatesById);
        var remainingCandidates = GetSchedulableCandidates(
            command.Preferences,
            candidatesById).ToList();

        var stops = new List<ItineraryStop>();
        var cursor = command.VisitStartAt;
        var previousAttractionId = command.StartingAttractionId;

        while (remainingCandidates.Count > 0)
        {
            var next = await SelectNextCandidateAsync(
                parkId,
                previousAttractionId,
                remainingCandidates,
                preferencesByAttraction,
                unscheduled,
                cancellationToken);
            if (next is null)
            {
                continue;
            }

            var candidate = next.Candidate;
            remainingCandidates.Remove(candidate);
            var preference = preferencesByAttraction[candidate.AttractionId];
            var queueMinutes = ExpectedQueueMinutes(candidate);
            var attractionDurationMinutes =
                candidate.DurationMinutes ?? DefaultAttractionDurationMinutes;
            var queueStartsAt = cursor.AddMinutes(next.WalkingMinutes);
            var attractionStartsAt = queueStartsAt.AddMinutes(queueMinutes);
            var completesAt = attractionStartsAt.AddMinutes(attractionDurationMinutes);

            if (completesAt > command.VisitEndAt)
            {
                unscheduled.Add(new UnscheduledAttraction(
                    candidate.AttractionId,
                    preference.Level,
                    UnscheduledAttractionReason.VisitWindowExceeded));
                continue;
            }

            stops.Add(new ItineraryStop(
                stops.Count + 1,
                candidate.AttractionId,
                candidate.AttractionName,
                preference.Level,
                cursor,
                next.WalkingMinutes,
                queueStartsAt,
                queueMinutes,
                attractionStartsAt,
                attractionDurationMinutes,
                completesAt));
            cursor = completesAt;
            previousAttractionId = candidate.AttractionId;
        }

        return new OptimizedItineraryResult(
            parkId,
            command.VisitStartAt,
            command.VisitEndAt,
            generatedAt,
            stops,
            unscheduled,
            stops.Sum(stop => stop.WalkingMinutes),
            stops.Sum(stop => stop.QueueMinutes),
            stops.Sum(stop => stop.AttractionDurationMinutes),
            AlgorithmVersion);
    }

    private static List<UnscheduledAttraction> CreateInitiallyUnscheduled(
        IReadOnlyList<ItineraryPreference> preferences,
        IReadOnlyDictionary<long, ItineraryCandidate> candidates)
    {
        var unscheduled = new List<UnscheduledAttraction>();

        foreach (var preference in preferences)
        {
            if (preference.Level == AttractionPreferenceLevel.Skip)
            {
                unscheduled.Add(new UnscheduledAttraction(
                    preference.AttractionId,
                    preference.Level,
                    UnscheduledAttractionReason.SkippedByVisitor));
                continue;
            }

            if (!candidates.TryGetValue(preference.AttractionId, out var candidate) ||
                !candidate.IsActive)
            {
                unscheduled.Add(new UnscheduledAttraction(
                    preference.AttractionId,
                    preference.Level,
                    UnscheduledAttractionReason.AttractionUnavailable));
                continue;
            }

            if (candidate.IsOpen == false)
            {
                unscheduled.Add(new UnscheduledAttraction(
                    preference.AttractionId,
                    preference.Level,
                    UnscheduledAttractionReason.AttractionClosed));
            }
        }

        return unscheduled;
    }

    private static IReadOnlyList<ItineraryCandidate> GetSchedulableCandidates(
        IReadOnlyList<ItineraryPreference> preferences,
        IReadOnlyDictionary<long, ItineraryCandidate> candidates)
    {
        var preferenceByAttraction = preferences.ToDictionary(
            preference => preference.AttractionId);

        return candidates.Values
            .Where(candidate =>
                candidate.IsActive &&
                candidate.IsOpen != false &&
                preferenceByAttraction[candidate.AttractionId].Level !=
                    AttractionPreferenceLevel.Skip)
            .ToArray();
    }

    private async Task<CandidateOption?> SelectNextCandidateAsync(
        long parkId,
        long? previousAttractionId,
        List<ItineraryCandidate> remainingCandidates,
        IReadOnlyDictionary<long, ItineraryPreference> preferences,
        List<UnscheduledAttraction> unscheduled,
        CancellationToken cancellationToken)
    {
        var highestPriority = remainingCandidates.Min(candidate =>
            PreferenceRank(preferences[candidate.AttractionId].Level));
        var options = new List<CandidateOption>();

        foreach (var candidate in remainingCandidates.Where(candidate =>
                     PreferenceRank(preferences[candidate.AttractionId].Level) ==
                     highestPriority).ToArray())
        {
            var walkingMinutes = await EstimateWalkingMinutesAsync(
                parkId,
                previousAttractionId,
                candidate.AttractionId,
                cancellationToken);
            if (walkingMinutes is not null)
            {
                options.Add(new CandidateOption(candidate, walkingMinutes.Value));
                continue;
            }

            remainingCandidates.Remove(candidate);
            unscheduled.Add(new UnscheduledAttraction(
                candidate.AttractionId,
                preferences[candidate.AttractionId].Level,
                UnscheduledAttractionReason.WalkingRouteUnavailable));
        }

        return options
            .OrderBy(option =>
                option.WalkingMinutes + ExpectedQueueMinutes(option.Candidate))
            .ThenBy(option => ExpectedQueueMinutes(option.Candidate))
            .ThenBy(option => option.Candidate.AttractionName, StringComparer.Ordinal)
            .ThenBy(option => option.Candidate.AttractionId)
            .FirstOrDefault();
    }

    private static int ExpectedQueueMinutes(ItineraryCandidate candidate) =>
        (candidate.WaitMinutes, candidate.HistoricalWaitMinutes) switch
        {
            (short current, short historical) =>
                (int)Math.Round(
                    (current * 2m + historical) / 3m,
                    MidpointRounding.AwayFromZero),
            (short current, null) => current,
            (null, short historical) => historical,
            _ => 0
        };

    private async Task<int?> EstimateWalkingMinutesAsync(
        long parkId,
        long? previousAttractionId,
        long attractionId,
        CancellationToken cancellationToken)
    {
        if (previousAttractionId is null)
        {
            return 0;
        }

        var estimate = await walkingTimeService.EstimateAsync(
            parkId,
            previousAttractionId.Value,
            attractionId,
            cancellationToken);
        return estimate?.Status == WalkingTimeEstimateStatus.Available
            ? estimate.EstimatedWalkingMinutes
            : null;
    }

    private static int PreferenceRank(AttractionPreferenceLevel preference) =>
        preference switch
        {
            AttractionPreferenceLevel.MustDo => 0,
            AttractionPreferenceLevel.WouldLike => 1,
            AttractionPreferenceLevel.Skip => 2,
            _ => throw new ArgumentOutOfRangeException(
                nameof(preference),
                preference,
                "Unsupported attraction preference.")
        };

    private sealed record CandidateOption(
        ItineraryCandidate Candidate,
        int WalkingMinutes);

    private static void Validate(long parkId, GenerateItineraryCommand command)
    {
        RequestGuard.RequireParkIdentifier(parkId);

        if (command.VisitEndAt <= command.VisitStartAt)
        {
            throw new ArgumentException(
                "Visit end time must be later than its start time.",
                nameof(command.VisitEndAt));
        }

        if (command.VisitEndAt - command.VisitStartAt > MaximumVisitWindow)
        {
            throw new ArgumentOutOfRangeException(
                nameof(command.VisitEndAt),
                $"Visit window cannot exceed {MaximumVisitWindow.TotalHours} hours.");
        }

        if (command.StartingAttractionId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(command.StartingAttractionId),
                "Starting attraction ID must be greater than zero.");
        }

        if (command.Preferences.Count == 0)
        {
            throw new ArgumentException(
                "Select at least one attraction.",
                nameof(command.Preferences));
        }

        if (command.Preferences.Any(preference => preference.AttractionId <= 0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(command.Preferences),
                "Attraction IDs must be greater than zero.");
        }

        if (command.Preferences.Any(preference => !Enum.IsDefined(preference.Level)))
        {
            throw new ArgumentOutOfRangeException(
                nameof(command.Preferences),
                "Attraction preference levels must be valid.");
        }

        if (command.Preferences.Select(preference => preference.AttractionId)
            .Distinct()
            .Count() != command.Preferences.Count)
        {
            throw new ArgumentException(
                "Each attraction can have only one preference.",
                nameof(command.Preferences));
        }
    }
}
