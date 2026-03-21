// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json.Serialization;

namespace EchoPhase.Clients.Twitch.Models
{
    public class TwitchApiPagination : ITwitchApiPagination
    {
        [JsonPropertyName("cursor")]
        public string? Cursor { get; set; } = string.Empty;
    }
}
