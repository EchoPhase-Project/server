using System.Text.Json.Serialization;

using EchoPhase.Clients.Twitch.Models;

namespace EchoPhase.Clients.Twitch.Models
{
    public class TwitchApiResponseDto<TR> : ITwitchApiResponseDto<TR>
    {
        [JsonPropertyName("data")]
        public TR? Data { get; set; } = default;

        [JsonPropertyName("pagination")]
        public ITwitchApiPagination? Pagination { get; set; } = default;
    }
}
