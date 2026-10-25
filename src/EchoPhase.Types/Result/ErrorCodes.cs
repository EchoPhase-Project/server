// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Types.Result
{
    /// <summary>
    /// Error codes every layer uses in the same sense; each layer's *Errors class
    /// refers to these, so one name is one value and one HTTP status throughout.
    /// Codes particular to a layer stay in its own class.
    /// </summary>
    public static class ErrorCodes
    {
        public const string NotFound = "NotFound";
        /// <summary>The request itself is wrong (validation, unknown values).</summary>
        public const string Invalid = "Invalid";
        public const string Conflict = "Conflict";
        /// <summary>Authenticated, but not allowed.</summary>
        public const string Forbidden = "Forbidden";
        /// <summary>Not (or wrongly) authenticated, e.g. a wrong webhook secret.</summary>
        public const string Unauthorized = "Unauthorized";
        /// <summary>Unknown user or wrong password (deliberately not told apart).</summary>
        public const string InvalidCredentials = "InvalidCredentials";
        /// <summary>A feature needs settings the deployment does not have (503).</summary>
        public const string NotConfigured = "NotConfigured";
    }
}
