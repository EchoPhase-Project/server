// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json.Serialization;

namespace EchoPhase.Clients.Twitch.Dto
{
    public class TwitchVipRequestQueryDto
    {
        [JsonPropertyName("broadcaster_id")]
        public string BroadcasterId { get; set; } = string.Empty;

        [JsonPropertyName("user_id")]
        public List<string>? UserIds
        {
            get; set;
        }

        [JsonPropertyName("first")]
        public int First { get; set; } = 20;

        [JsonPropertyName("after")]
        public string? After
        {
            get; set;
        }
    }
}
