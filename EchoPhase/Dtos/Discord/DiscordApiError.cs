using System.Text.Json.Serialization;

using EchoPhase.Interfaces;

namespace EchoPhase.Dtos
{
    public class DiscordApiError : IDiscordApiError
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("errors")]
        public Dictionary<string, string[]>? Errors { get; set; }
    }
}

