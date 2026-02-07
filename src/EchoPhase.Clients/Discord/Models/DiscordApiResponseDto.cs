using System.Text.Json.Serialization;

namespace EchoPhase.Clients.Discord.Models
{
    public class DiscordApiResponseDto<T> : IDiscordApiResponseDto<T>
    {
        [JsonPropertyName("data")]
        public T? Data
        {
            get; set;
        }

        [JsonPropertyName("message")]
        public string? Message
        {
            get; set;
        }
    }
}
