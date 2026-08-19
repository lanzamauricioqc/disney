using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Disney.Application;
using Microsoft.Extensions.Logging;

namespace Disney.Infrastructure;

internal sealed class QueueTimesClient(
    HttpClient httpClient,
    ILogger<QueueTimesClient> logger) : IQueueTimesProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<QueueTimesSnapshot> GetQueueTimesForParkAsync(
        int sourceParkId,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using var response = await httpClient.GetAsync(
            $"/parks/{sourceParkId}/queue_times.json",
            cancellationToken);

        logger.LogInformation(
            "Queue-times request for park {SourceParkId} returned {StatusCode} in {ElapsedMilliseconds} ms.",
            sourceParkId,
            (int)response.StatusCode,
            stopwatch.ElapsedMilliseconds);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<QueueTimesResponse>(
            JsonOptions,
            cancellationToken)
            ?? throw new InvalidDataException(
                $"Queue-times response for source park {sourceParkId} was empty.");

        var rideCount = payload.Lands.Sum(land => land.Rides.Count) + payload.Rides.Count;
        if (rideCount == 0)
        {
            throw new InvalidDataException(
                $"Queue-times response for source park {sourceParkId} contained no rides.");
        }

        return new QueueTimesSnapshot(
            payload.Lands.Select(MapLand).ToList(),
            payload.Rides.Select(MapRide).ToList());
    }

    private static QueueLandSnapshot MapLand(QueueLandResponse land) =>
        new(
            land.Id,
            land.Name,
            land.Rides.Select(MapRide).ToList());

    private static QueueRideSnapshot MapRide(QueueRideResponse ride) =>
        new(ride.Id, ride.Name, ride.IsOpen, ride.WaitTime, ride.LastUpdated);

    private sealed class QueueTimesResponse
    {
        [JsonPropertyName("lands")]
        public List<QueueLandResponse> Lands { get; set; } = [];

        [JsonPropertyName("rides")]
        public List<QueueRideResponse> Rides { get; set; } = [];
    }

    private sealed class QueueLandResponse
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("rides")]
        public List<QueueRideResponse> Rides { get; set; } = [];
    }

    private sealed class QueueRideResponse
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("is_open")]
        public bool IsOpen { get; set; }

        [JsonPropertyName("wait_time")]
        public int WaitTime { get; set; }

        [JsonPropertyName("last_updated")]
        public DateTimeOffset LastUpdated { get; set; }
    }
}
