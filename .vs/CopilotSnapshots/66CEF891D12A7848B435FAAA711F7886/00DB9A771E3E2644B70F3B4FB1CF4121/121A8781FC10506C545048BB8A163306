using System.Text.Json.Serialization;

namespace WorkerModels
{
    public class WaitingTimeModel
    {
        [JsonPropertyName("lands")]
        public List<Land> Lands { get; set; } = [];

        [JsonPropertyName("rides")]
        public List<Ride> Rides { get; set; } = [];
    }

    public class Land
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("rides")]
        public List<Ride> Rides { get; set; } = [];
    }

    public class Ride
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