namespace Disney.Application;

public sealed class QueueAnalyticsService(
    IQueueAnalyticsReader reader,
    TimeProvider timeProvider) : IQueueAnalyticsService
{
    private const int LookbackMonths = 3;
    public static readonly TimeSpan MaximumHistoricalQueryWindow = TimeSpan.FromDays(31);

    public async Task<CurrentWaitTimesResult> GetCurrentWaitTimesAsync(
        long parkId,
        CancellationToken cancellationToken)
    {
        ValidateParkId(parkId);
        var window = CreateWindow();
        var waits = await reader.GetCurrentWaitTimesAsync(
            parkId,
            window.From,
            window.To,
            cancellationToken);
        return new CurrentWaitTimesResult(parkId, window.From, window.To, waits);
    }

    public async Task<WeekdayWaitTimePatternsResult> GetWeekdayWaitTimePatternsAsync(
        long parkId,
        long? attractionId,
        CancellationToken cancellationToken)
    {
        Validate(parkId, attractionId);
        var window = CreateWindow();
        var patterns = await reader.GetWeekdayWaitTimePatternsAsync(
            parkId,
            attractionId,
            window.From,
            window.To,
            cancellationToken);
        return new WeekdayWaitTimePatternsResult(parkId, window.From, window.To, patterns);
    }

    public async Task<DailyWaitTimeHistoryResult> GetDailyWaitTimeHistoryAsync(
        long parkId,
        long attractionId,
        CancellationToken cancellationToken)
    {
        Validate(parkId, attractionId);
        var window = CreateWindow();
        var history = await reader.GetDailyWaitTimeHistoryAsync(
            parkId,
            attractionId,
            window.From,
            window.To,
            cancellationToken);
        return new DailyWaitTimeHistoryResult(
            parkId,
            attractionId,
            window.From,
            window.To,
            history);
    }

    public async Task<HistoricalWaitTimesResult> GetHistoricalWaitTimesAsync(
        long parkId,
        long attractionId,
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        CancellationToken cancellationToken)
    {
        Validate(parkId, attractionId);
        ValidateHistoricalWindow(fromInclusive, toExclusive);
        var observations = await reader.GetHistoricalWaitTimesAsync(
            parkId,
            attractionId,
            fromInclusive,
            toExclusive,
            cancellationToken);
        return new HistoricalWaitTimesResult(
            parkId,
            attractionId,
            fromInclusive,
            toExclusive,
            observations);
    }

    public async Task<WeekdayClosurePatternsResult> GetWeekdayClosurePatternsAsync(
        long parkId,
        long? attractionId,
        CancellationToken cancellationToken)
    {
        Validate(parkId, attractionId);
        var window = CreateWindow();
        var patterns = await reader.GetWeekdayClosurePatternsAsync(
            parkId,
            attractionId,
            window.From,
            window.To,
            cancellationToken);
        return new WeekdayClosurePatternsResult(parkId, window.From, window.To, patterns);
    }

    private (DateTimeOffset From, DateTimeOffset To) CreateWindow()
    {
        var windowEnd = timeProvider.GetUtcNow();
        return (windowEnd.AddMonths(-LookbackMonths), windowEnd);
    }

    private static void Validate(long parkId, long? attractionId)
    {
        ValidateParkId(parkId);
        if (attractionId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attractionId),
                "Attraction id must be greater than zero.");
        }
    }

    private static void ValidateParkId(long parkId)
    {
        if (parkId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(parkId),
                "Park id must be greater than zero.");
        }
    }

    private static void ValidateHistoricalWindow(
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive)
    {
        if (fromInclusive >= toExclusive)
        {
            throw new ArgumentException(
                "The historical query start must be before its end.",
                nameof(fromInclusive));
        }

        if (toExclusive - fromInclusive > MaximumHistoricalQueryWindow)
        {
            throw new ArgumentOutOfRangeException(
                nameof(toExclusive),
                $"Historical queries cannot exceed {MaximumHistoricalQueryWindow.TotalDays} days.");
        }
    }
}
