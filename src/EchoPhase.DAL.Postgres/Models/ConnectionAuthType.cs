// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.DAL.Postgres.Models
{
    public enum ConnectionAuthType
    {
        /// <summary>Static token that cannot be refreshed (e.g. Discord bot token).</summary>
        BotToken = 0,

        /// <summary>OAuth 2.0 user token with an optional refresh token.</summary>
        OAuth2 = 1
    }
}
