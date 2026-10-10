// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json.Serialization;

namespace EchoPhase.Clients.Twitch.Dto
{
    /// <summary>
    /// Response of <c>GET https://id.twitch.tv/oauth2/validate</c>.
    /// </summary>
    public class TwitchTokenValidationDto
    {
        [JsonPropertyName("client_id")]
        public string ClientId { get; set; } = string.Empty;

        [JsonPropertyName("login")]
        public string? Login
        {
            get; set;
        }

        [JsonPropertyName("user_id")]
        public string? UserId
        {
            get; set;
        }

        [JsonPropertyName("scopes")]
        public List<string> Scopes { get; set; } = new();

        /// <summary>
        /// Seconds until the token expires; <c>0</c> for non-expiring tokens.
        /// </summary>
        [JsonPropertyName("expires_in")]
        public int ExpiresIn
        {
            get; set;
        }
    }
}
