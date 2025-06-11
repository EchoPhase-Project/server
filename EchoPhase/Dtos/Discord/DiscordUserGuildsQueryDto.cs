using System.Text.Json.Serialization;

namespace EchoPhase.Dtos
{
    public class DiscordUserGuildsQueryDto
    {
        [JsonPropertyName("before")]
        public string? Before { get; set; } = null;

        [JsonPropertyName("after")]
        public string? After { get; set; } = null;

        [JsonPropertyName("limit")]
        public int Limit { get; set; } = 200;

        [JsonPropertyName("with_counts")]
        public bool WithCounts { get; set; } = false;
    }
}
