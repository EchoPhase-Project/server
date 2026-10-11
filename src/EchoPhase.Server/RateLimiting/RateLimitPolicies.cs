// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Server.RateLimiting
{
    public static class RateLimitPolicies
    {
        /// <summary>
        /// Anonymous credential endpoints, limited per client IP.
        /// </summary>
        public const string Auth = "auth";
    }
}
