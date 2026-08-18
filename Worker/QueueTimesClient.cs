using System.Net.Http.Json;
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
        logger.LogDebug("Fetching queue times for source park {ParkId}.", sourceParkId);

        using var response = await httpClient.GetAsync(
            $"/parks/{sourceParkId}/queue_times.json",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<WaitingTimeModel>(
            JsonOptions,
            cancellationToken)
            ?? throw new InvalidDataException(
                $"Queue-times response for source park {sourceParkId} was empty.");
    }
}