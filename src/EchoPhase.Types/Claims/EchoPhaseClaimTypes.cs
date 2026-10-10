// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Types.Claims
{
    /// <summary>
    /// Claim types shared between token issuing (authentication) and policy
    /// evaluation (authorization).
    /// </summary>
    public static class EchoPhaseClaimTypes
    {
        /// <summary>
        /// Present when the token was issued to an API client rather than an
        /// interactive session (RFC 8693 <c>client_id</c>).
        /// </summary>
        public const string ClientId = "client_id";

        /// <summary>
        /// The party actually acting when the token is used on someone else's
        /// behalf: <c>{"sub": "&lt;actor id&gt;"}</c> (RFC 8693 §4.1).
        /// </summary>
        public const string Act = "act";

        /// <summary>
        /// Id of the impersonation record backing an <see cref="Act"/> token.
        /// </summary>
        public const string ImpersonationId = "imp";
    }
}
