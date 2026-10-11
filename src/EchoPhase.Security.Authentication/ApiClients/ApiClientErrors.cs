// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Types.Result;

namespace EchoPhase.Security.Authentication.ApiClients
{
    public static class ApiClientErrors
    {
        public const string NotFound = ErrorCodes.NotFound;
        public const string Invalid = ErrorCodes.Invalid;

        /// <summary>Unknown client, wrong secret or expired client (deliberately indistinguishable).</summary>
        public const string InvalidClient = "InvalidClient";
    }
}
