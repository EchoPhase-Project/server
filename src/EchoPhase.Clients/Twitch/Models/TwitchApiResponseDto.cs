// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json.Serialization;

namespace EchoPhase.Clients.Twitch.Models
{
    public class TwitchApiResponseDto<TR> : ITwitchApiResponseDto<IEnumerable<TR>>
    {
        [JsonPropertyName("data")]
        public IEnumerable<TR> Data { get; set; } = Enumerable.Empty<TR>();

        [JsonPropertyName("pagination")]
        public ITwitchApiPagination? Pagination { get; set; } = default;
    }
}
