using System.Text.Json.Serialization;

namespace EchoPhase.Clients.Twitch.Models
{
    public class TwitchApiPagination : ITwitchApiPagination
    {
        [JsonPropertyName("cursor")]
        public string? Cursor { get; set; } = string.Empty;
    }
}
