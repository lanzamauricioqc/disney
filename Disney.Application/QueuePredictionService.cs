namespace Disney.Application;

public sealed class QueuePredictionService(
    IQueuePredictionReader reader,
    TimeProvider timeProvider) : IQueuePredictionService
{
    private const int MinimumHistoricalSamples = QueueHistoryWindow.MinimumSampleCount;
    private const int FullSampleConfidenceCount = 12;
    private const string AlgorithmVersion = "weekday-quarter-hour-median-v1";
    public static readonly TimeSpan MaximumPredictionHorizon = TimeSpan.FromDays(1);

    public async Task<WaitTimePredictionResult?> PredictWaitTimeAsync(
        long parkId,
        long attractionId,
        DateTimeOffset targetAt,
        CancellationToken cancellationToken)
    {
        var generatedAt = timeProvider.GetUtcNow();
        Validate(parkId, attractionId, targetAt, generatedAt);

        var predictionData = await reader.GetPredictionDataAsync(
            parkId,
            attractionId,
            targetAt,
            generatedAt.AddMonths(-QueueHistoryWindow.LookbackMonths),
            generatedAt,
            cancellationToken);

        if (predictionData is null)
        {
            return null;
        }

        var samples = predictionData.HistoricalWaitMinutes;
        if (samples.Count < MinimumHistoricalSamples)
        {
            return CreateResult(
                parkId,
                predictionData,
                targetAt,
                generatedAt,
                WaitTimePredictionStatus.InsufficientHistoricalData,
                null,
                null);
        }

        var predictedWaitMinutes = CalculateMedian(samples);
        return CreateResult(
            parkId,
            predictionData,
            targetAt,
            generatedAt,
            WaitTimePredictionStatus.Available,
            predictedWaitMinutes,
            CalculateConfidenceScore(samples, predictedWaitMinutes));
    }

    private static WaitTimePredictionResult CreateResult(
        long parkId,
        QueuePredictionData predictionData,
        DateTimeOffset targetAt,
        DateTimeOffset generatedAt,
        WaitTimePredictionStatus status,
        short? predictedWaitMinutes,
        decimal? confidenceScore) =>
        new(
            parkId,
            predictionData.AttractionId,
            predictionData.AttractionName,
            targetAt,
            generatedAt,
            status,
            predictedWaitMinutes,
            confidenceScore,
            predictionData.HistoricalWaitMinutes.Count,
            AlgorithmVersion);

    private static short CalculateMedian(IReadOnlyList<short> samples)
    {
        var orderedSamples = samples.Order().ToArray();
        var middleIndex = orderedSamples.Length / 2;
        if (orderedSamples.Length % 2 != 0)
        {
            return orderedSamples[middleIndex];
        }

        var median = (orderedSamples[middleIndex - 1] + orderedSamples[middleIndex]) / 2m;
        return checked((short)Math.Round(median, MidpointRounding.AwayFromZero));
    }

    private static decimal CalculateConfidenceScore(
        IReadOnlyList<short> samples,
        short predictedWaitMinutes)
    {
        var sampleConfidence = Math.Min(
            samples.Count / (decimal)FullSampleConfidenceCount,
            1m);
        var averageAbsoluteDeviation = samples
            .Average(sample => (decimal)Math.Abs(sample - predictedWaitMinutes));
        var relativeDeviation = averageAbsoluteDeviation / Math.Max(predictedWaitMinutes, 1m);
        var consistencyConfidence = 1m - Math.Min(relativeDeviation, 1m);

        return Math.Round(
            sampleConfidence * consistencyConfidence,
            2,
            MidpointRounding.AwayFromZero);
    }

    private static void Validate(
        long parkId,
        long attractionId,
        DateTimeOffset targetAt,
        DateTimeOffset generatedAt)
    {
        RequestGuard.RequireParkIdentifier(parkId);
        RequestGuard.RequireAttractionIdentifier(attractionId);

        if (targetAt <= generatedAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetAt),
                "Prediction time must be in the future.");
        }

        if (targetAt - generatedAt > MaximumPredictionHorizon)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetAt),
                $"Prediction time cannot be more than " +
                $"{MaximumPredictionHorizon.TotalHours} hours in the future.");
        }
    }
}
