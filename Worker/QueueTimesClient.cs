using System.Net.Http.Json;
using System.Diagnostics;
using System.Text.Json;

namespace WorkerModels;

public sealed class QueueTimesClient(
    HttpClient httpClient,
    ILogger<QueueTimesClient> logger) : IQueueTimesProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<WaitingTimeModel> GetQueueTimesForParkAsync(
        int sourceParkId,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation(
            LogEvents.QueueTimesRequestStarted,
            "Queue-times request started for source park {SourceParkId}.",
            sourceParkId);

        using var response = await httpClient.GetAsync(
            $"/parks/{sourceParkId}/queue_times.json",
            cancellationToken);

        logger.LogInformation(
            LogEvents.QueueTimesRequestCompleted,
            "Queue-times request completed for source park {SourceParkId} with status {StatusCode} in {ElapsedMs} ms.",
            sourceParkId,
            (int)response.StatusCode,
            stopwatch.ElapsedMilliseconds);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                LogEvents.QueueTimesRequestRejected,
                "Queue-times request was rejected for source park {SourceParkId} with status {StatusCode}.",
                sourceParkId,
                (int)response.StatusCode);
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<WaitingTimeModel>(
            JsonOptions,
            cancellationToken)
            ?? throw new InvalidDataException(
                $"Queue-times response for source park {sourceParkId} was empty.");
    }
}