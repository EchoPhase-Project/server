// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Security.Authentication.Jwt.Providers
{
    public class TokenPair
    {
        public string AccessToken { get; set; } = string.Empty;

        /// <summary>
        /// The rotated refresh token; <c>null</c> when a parallel request already
        /// rotated it and the caller must keep the token from that response.
        /// </summary>
        public string? RefreshToken { get; set; }
    }
}
