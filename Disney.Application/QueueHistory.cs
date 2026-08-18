namespace Disney.Application;

public sealed record QueueHistoryQuery(
    long ParkId,
    DateTimeOffset From,
    DateTimeOffset To,
    long? AttractionId = null,
    int Limit = 500);

public sealed record QueueHistoryPoint(
    long AttractionId,
    string AttractionName,
    DateTimeOffset ObservedAt,
    bool IsOpen,
    short? WaitMinutes);

public sealed record QueueHourlySummary(
    long AttractionId,
    string AttractionName,
    DateOnly LocalDate,
    short LocalHour,
    decimal AverageWaitMinutes,
    short MaximumWaitMinutes,
    int ObservationCount);

public interface IQueueHistoryReader
{
    Task<IReadOnlyList<QueueHistoryPoint>> GetHistoryAsync(
        QueueHistoryQuery query,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<QueueHourlySummary>> GetHourlySummaryAsync(
        QueueHistoryQuery query,
        CancellationToken cancellationToken);
}
