// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json.Serialization;

namespace EchoPhase.Clients.Discord.Models
{
    public class DiscordApiError : IDiscordApiError
    {
        [JsonPropertyName("code")]
        public int Code
        {
            get; set;
        }

        [JsonPropertyName("message")]
        public string? Message
        {
            get; set;
        }

        [JsonPropertyName("errors")]
        public Dictionary<string, string[]>? Errors
        {
            get; set;
        }
    }
}

