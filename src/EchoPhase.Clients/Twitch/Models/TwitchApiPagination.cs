using System.Text.Json.Serialization;

using EchoPhase.Clients.Twitch.Models;

namespace EchoPhase.Clients.Twitch.Models
{
    public class TwitchApiPagination : ITwitchApiPagination
    {
        [JsonPropertyName("cursor")]
        public string? Cursor { get; set; } = string.Empty;
    }
}
