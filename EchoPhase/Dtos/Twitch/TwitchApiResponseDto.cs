using System.Text.Json.Serialization;

using EchoPhase.Interfaces;

namespace EchoPhase.Dtos
{
    public class TwitchApiResponseDto<TR> : ITwitchApiResponseDto<TR>
    {
        [JsonPropertyName("data")]
        public TR? Data { get; set; } = default;

        [JsonPropertyName("pagination")]
        public ITwitchApiPagination? Pagination { get; set; } = default;
    }
}
