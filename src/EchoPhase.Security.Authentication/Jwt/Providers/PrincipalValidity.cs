// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Security.Authentication.Jwt.Providers
{
    /// <summary>What a re-check of a long-lived connection's principal found.</summary>
    public enum PrincipalValidity
    {
        Valid,

        /// <summary>Expired or revoked (security stamp, user, API client, impersonation).</summary>
        Invalid,

        /// <summary>
        /// The session the token was issued for is gone (signed out, revoked):
        /// the client must not reconnect with it.
        /// </summary>
        SessionEnded
    }
}
