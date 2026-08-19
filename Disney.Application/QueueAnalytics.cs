namespace Disney.Application;

public sealed class CurrentWaitTime
{
    public long AttractionId { get; init; }
    public string AttractionName { get; init; } = string.Empty;
    public long? LandId { get; init; }
    public string? LandName { get; init; }
    public DateTimeOffset ObservedAt { get; init; }
    public bool IsOpen { get; init; }
    public short? WaitMinutes { get; init; }
}

public sealed record WeekdayWaitTimePattern(
    long AttractionId,
    string AttractionName,
    DayOfWeek DayOfWeek,
    short LocalHour,
    decimal AverageWaitMinutes,
    decimal MedianWaitMinutes,
    short MinimumWaitMinutes,
    short MaximumWaitMinutes,
    int ObservationCount);

public sealed record WeekdayClosurePattern(
    long AttractionId,
    string AttractionName,
    DayOfWeek DayOfWeek,
    short LocalHour,
    int ClosedObservationCount,
    int TotalObservationCount,
    decimal ClosedPercentage);

public sealed record CurrentWaitTimesResult(
    long ParkId,
    DateTimeOffset WindowStart,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<CurrentWaitTime> Attractions);

public sealed record WeekdayWaitTimePatternsResult(
    long ParkId,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    IReadOnlyList<WeekdayWaitTimePattern> Patterns);

public sealed record WeekdayClosurePatternsResult(
    long ParkId,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    IReadOnlyList<WeekdayClosurePattern> Patterns);

public interface IQueueAnalyticsReader
{
    Task<IReadOnlyList<CurrentWaitTime>> GetCurrentWaitTimesAsync(
        long parkId,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<WeekdayWaitTimePattern>> GetWeekdayWaitTimePatternsAsync(
        long parkId,
        long? attractionId,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<WeekdayClosurePattern>> GetWeekdayClosurePatternsAsync(
        long parkId,
        long? attractionId,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken cancellationToken);
}

public interface IQueueAnalyticsService
{
    Task<CurrentWaitTimesResult> GetCurrentWaitTimesAsync(
        long parkId,
        CancellationToken cancellationToken);

    Task<WeekdayWaitTimePatternsResult> GetWeekdayWaitTimePatternsAsync(
        long parkId,
        long? attractionId,
        CancellationToken cancellationToken);

    Task<WeekdayClosurePatternsResult> GetWeekdayClosurePatternsAsync(
        long parkId,
        long? attractionId,
        CancellationToken cancellationToken);
}
