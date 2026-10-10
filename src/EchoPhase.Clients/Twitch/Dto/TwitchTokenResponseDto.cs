// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json.Serialization;

namespace EchoPhase.Clients.Twitch.Dto
{
    /// <summary>
    /// Response of <c>POST https://id.twitch.tv/oauth2/token</c> for both the
    /// <c>authorization_code</c> and <c>refresh_token</c> grants.
    /// </summary>
    public class TwitchTokenResponseDto
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken
        {
            get; set;
        }

        [JsonPropertyName("scope")]
        public List<string> Scopes { get; set; } = new();

        [JsonPropertyName("expires_in")]
        public int ExpiresIn
        {
            get; set;
        }
    }
}
