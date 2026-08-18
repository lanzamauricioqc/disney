using System;
using System.Text.Json.Serialization;

namespace Repositories
{
    public class Attraction
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("park_id")]
        public int ParkId { get; set; }

        [JsonPropertyName("current_land_id")]
        public int? CurrentLandId { get; set; }

        [JsonPropertyName("source_ride_id")]
        public int SourceRideId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("is_active")]
        public bool IsActive { get; set; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
