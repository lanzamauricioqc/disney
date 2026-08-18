using System;
using System.Text.Json.Serialization;

namespace Repositories
{
    public class Park
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("source_park_id")]
        public int SourceParkId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("timezone")]
        public string Timezone { get; set; } = string.Empty;

        [JsonPropertyName("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; }
    }
}