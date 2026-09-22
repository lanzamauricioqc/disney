namespace Disney.Application;

public sealed class ItineraryOptimizationService(
    IItineraryCandidateReader candidateReader,
    IWalkingTimeService walkingTimeService,
    TimeProvider timeProvider) : IItineraryOptimizationService
{
    private const int DefaultAttractionDurationMinutes = 10;
    private const string AlgorithmVersion = "priority-current-wait-greedy-v1";
    public static readonly TimeSpan MaximumVisitWindow = TimeSpan.FromDays(1);

    public async Task<OptimizedItineraryResult> GenerateAsync(
        long parkId,
        GenerateItineraryCommand command,
        CancellationToken cancellationToken)
    {
        Validate(parkId, command);
        var preferencesByAttraction = command.Preferences.ToDictionary(
            preference => preference.AttractionId);
        var candidates = await candidateReader.GetCandidatesAsync(
            parkId,
            preferencesByAttraction.Keys.ToArray(),
            cancellationToken);
        var candidatesById = candidates.ToDictionary(candidate => candidate.AttractionId);
        var unscheduled = CreateInitiallyUnscheduled(
            command.Preferences,
            candidatesById);
        var schedulableCandidates = OrderCandidates(
            command.Preferences,
            candidatesById);

        var stops = new List<ItineraryStop>();
        var cursor = command.VisitStartAt;
        var previousAttractionId = command.StartingAttractionId;

        foreach (var candidate in schedulableCandidates)
        {
            var preference = preferencesByAttraction[candidate.AttractionId];
            var walkingMinutes = await EstimateWalkingMinutesAsync(
                parkId,
                previousAttractionId,
                candidate.AttractionId,
                cancellationToken);
            if (walkingMinutes is null)
            {
                unscheduled.Add(new UnscheduledAttraction(
                    candidate.AttractionId,
                    preference.Level,
                    UnscheduledAttractionReason.WalkingRouteUnavailable));
                continue;
            }

            var queueMinutes = candidate.WaitMinutes ?? 0;
            var attractionDurationMinutes =
                candidate.DurationMinutes ?? DefaultAttractionDurationMinutes;
            var queueStartsAt = cursor.AddMinutes(walkingMinutes.Value);
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
                walkingMinutes.Value,
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
            timeProvider.GetUtcNow(),
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

    private static IReadOnlyList<ItineraryCandidate> OrderCandidates(
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
            .OrderBy(candidate =>
                PreferenceRank(preferenceByAttraction[candidate.AttractionId].Level))
            .ThenBy(candidate => candidate.WaitMinutes ?? short.MaxValue)
            .ThenBy(candidate => candidate.AttractionName, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.AttractionId)
            .ToArray();
    }

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

    private static void Validate(long parkId, GenerateItineraryCommand command)
    {
        if (parkId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(parkId),
                "Park id must be greater than zero.");
        }

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
                "Starting attraction id must be greater than zero.");
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
                "Attraction ids must be greater than zero.");
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
