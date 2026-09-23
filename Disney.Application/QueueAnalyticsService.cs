namespace Disney.Application;

public sealed class QueueAnalyticsService(
    IQueueAnalyticsReader reader,
    TimeProvider timeProvider) : IQueueAnalyticsService
{
    private const int DaysInWeek = 7;
    private static readonly TimeZoneInfo ParkTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    public static readonly TimeSpan MaximumHistoricalQueryWindow = TimeSpan.FromDays(31);

    public async Task<CurrentWaitTimesResult> GetCurrentWaitTimesAsync(
        long parkId,
        CancellationToken cancellationToken)
    {
        RequestGuard.RequireParkIdentifier(parkId);
        var windowEnd = timeProvider.GetUtcNow();
        var windowStart = windowEnd.Subtract(
            QueueDataFreshness.MaximumLiveObservationAge);
        var waits = await reader.GetCurrentWaitTimesAsync(
            parkId,
            windowStart,
            windowEnd,
            cancellationToken);
        return new CurrentWaitTimesResult(parkId, windowStart, windowEnd, waits);
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

    public async Task<DailyParkWaitTimesResult> GetDailyParkWaitTimesAsync(
        DateOnly? weekStart,
        CancellationToken cancellationToken)
    {
        var generatedAt = timeProvider.GetUtcNow();
        var currentDate = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(generatedAt, ParkTimeZone).DateTime);
        var currentWeekStart = StartOfWeek(currentDate);
        var selectedWeekStart = weekStart ?? currentWeekStart;
        var availableFrom = currentDate.AddMonths(-QueueHistoryWindow.LookbackMonths);

        ValidateWeek(selectedWeekStart, availableFrom, currentWeekStart);

        var queryStart = selectedWeekStart < availableFrom
            ? availableFrom
            : selectedWeekStart;
        var weekEnd = selectedWeekStart.AddDays(DaysInWeek - 1);
        var parks = await reader.GetDailyParkWaitTimesAsync(
            queryStart,
            weekEnd.AddDays(1),
            cancellationToken);

        return new DailyParkWaitTimesResult(
            selectedWeekStart,
            weekEnd,
            availableFrom,
            currentWeekStart,
            generatedAt,
            parks);
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
        return (windowEnd.AddMonths(-QueueHistoryWindow.LookbackMonths), windowEnd);
    }

    private static void Validate(long parkId, long? attractionId)
    {
        RequestGuard.RequireParkIdentifier(parkId);
        RequestGuard.RequireOptionalAttractionIdentifier(attractionId);
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

    private static void ValidateWeek(
        DateOnly weekStart,
        DateOnly availableFrom,
        DateOnly currentWeekStart)
    {
        if (weekStart.DayOfWeek != DayOfWeek.Monday)
        {
            throw new ArgumentException(
                "The selected week must start on a Monday.",
                nameof(weekStart));
        }

        if (weekStart > currentWeekStart)
        {
            throw new ArgumentOutOfRangeException(
                nameof(weekStart),
                "The selected week cannot be in the future.");
        }

        if (weekStart.AddDays(DaysInWeek - 1) < availableFrom)
        {
            throw new ArgumentOutOfRangeException(
                nameof(weekStart),
                "The selected week is outside the three-month history window.");
        }
    }

    private static DateOnly StartOfWeek(DateOnly date) =>
        date.AddDays(-((7 + (int)date.DayOfWeek - (int)DayOfWeek.Monday) % 7));
}
