using System.Text.Json.Serialization;

namespace EchoPhase.Dtos
{
	public class TwitchVipResponseDto
    {
        [JsonPropertyName("user_id")]
        public string UserId { get; init; } = default!;

        [JsonPropertyName("user_name")]
        public string UserName { get; init; } = default!;

        [JsonPropertyName("user_login")]
        public string UserLogin { get; init; } = default!;
    }
}
