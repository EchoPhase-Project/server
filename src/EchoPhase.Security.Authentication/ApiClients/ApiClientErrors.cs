// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Security.Authentication.ApiClients
{
    public static class ApiClientErrors
    {
        public const string NotFound = "NotFound";
        public const string Invalid = "Invalid";

        /// <summary>Unknown client, wrong secret or expired client (deliberately indistinguishable).</summary>
        public const string InvalidClient = "InvalidClient";
    }
}
