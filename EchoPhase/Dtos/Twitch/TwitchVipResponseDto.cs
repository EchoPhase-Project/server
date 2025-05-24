using System.Text.Json.Serialization;

namespace EchoPhase.Dtos
{
	public class TwitchVipResponseDto
    {
        [JsonPropertyName("user_id")]
        public string UserId { get; set; } = string.Empty;

        [JsonPropertyName("user_name")]
        public string UserName { get; set; } = string.Empty;

        [JsonPropertyName("user_login")]
        public string UserLogin { get; set; } = string.Empty;
    }
}
