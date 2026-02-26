using System.Text.Json.Serialization;

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
