// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Types.Result;

namespace EchoPhase.Identity
{
    /// <summary>Error codes of assigning and revoking roles.</summary>
    public static class RoleErrors
    {
        /// <summary>A role name that is not one of the roles defined in code.</summary>
        public const string UnknownRole = "UnknownRole";
        /// <summary>Identity refused the change (its descriptions are in the message).</summary>
        public const string Invalid = ErrorCodes.Invalid;
    }
}
