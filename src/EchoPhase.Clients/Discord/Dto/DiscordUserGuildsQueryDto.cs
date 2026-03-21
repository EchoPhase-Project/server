// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json.Serialization;

namespace EchoPhase.Clients.Discord.Dto
{
    public class DiscordUserGuildsQueryDto
    {
        [JsonPropertyName("before")]
        public string? Before { get; set; } = null;

        [JsonPropertyName("after")]
        public string? After { get; set; } = null;

        [JsonPropertyName("limit")]
        public int Limit { get; set; } = 200;

        [JsonPropertyName("with_counts")]
        public bool WithCounts { get; set; } = false;
    }
}
