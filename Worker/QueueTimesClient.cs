using System.Net.Http.Json;
using System.Text.Json;

namespace WorkerModels
{
    public class QueueTimesClient : IQueueTimesProvider
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<QueueTimesClient> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public QueueTimesClient(
            HttpClient httpClient,
            ILogger<QueueTimesClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<WaitingTimeModel?> GetQueueTimesForParkAsync(int sourceParkId, CancellationToken cancellationToken)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/parks/{sourceParkId}/queue_times.json", cancellationToken);

                response.EnsureSuccessStatusCode();

                return await response.Content.ReadFromJsonAsync<WaitingTimeModel>(JsonOptions, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching queue times for park {ParkId}.", sourceParkId);
                return null;
            }
        }

    }
}