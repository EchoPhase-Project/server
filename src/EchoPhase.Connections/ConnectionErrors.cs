// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Types.Result;

namespace EchoPhase.Connections
{
    /// <summary>
    /// Error codes carried by <c>IServiceResult</c> failures from this project.
    /// </summary>
    public static class ConnectionErrors
    {
        public const string NotFound = ErrorCodes.NotFound;
        public const string UnknownProvider = "UnknownProvider";
        public const string Invalid = ErrorCodes.Invalid;
        public const string Conflict = ErrorCodes.Conflict;
        public const string ReauthRequired = "ReauthRequired";

        /// <summary>The token works but the provider refused the request (e.g. missing scope).</summary>
        public const string Forbidden = ErrorCodes.Forbidden;

        /// <summary>The provider could not be reached or failed; nothing was changed.</summary>
        public const string Upstream = "Upstream";

        /// <summary>The required OAuth application is not configured.</summary>
        public const string NotConfigured = "NotConfigured";
    }
}
