namespace Disney.Application;

public enum WaitTimePredictionStatus
{
    Available,
    InsufficientHistoricalData
}

public sealed record QueuePredictionData(
    long AttractionId,
    string AttractionName,
    IReadOnlyList<short> HistoricalWaitMinutes);

public sealed record WaitTimePredictionResult(
    long ParkId,
    long AttractionId,
    string AttractionName,
    DateTimeOffset TargetAt,
    DateTimeOffset GeneratedAt,
    WaitTimePredictionStatus Status,
    short? PredictedWaitMinutes,
    decimal? ConfidenceScore,
    int HistoricalSampleCount,
    string AlgorithmVersion);

public interface IQueuePredictionReader
{
    Task<QueuePredictionData?> GetPredictionDataAsync(
        long parkId,
        long attractionId,
        DateTimeOffset targetAt,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken cancellationToken);
}

public interface IQueuePredictionService
{
    Task<WaitTimePredictionResult?> PredictWaitTimeAsync(
        long parkId,
        long attractionId,
        DateTimeOffset targetAt,
        CancellationToken cancellationToken);
}
