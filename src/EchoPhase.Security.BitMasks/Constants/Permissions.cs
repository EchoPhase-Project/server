// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Security.BitMasks.Constants
{
    /// <summary>
    /// Actions on a resource. Kept to what endpoints actually check.
    /// </summary>
    public class Permissions : ConstantsBase<Permissions>
    {
        public const string Read = "read";
        public const string Create = "create";
        public const string Update = "update";
        public const string Delete = "delete";
        public const string Execute = "execute";
        public const string Connect = "connect";
        public const string Assign = "assign";
        public const string Impersonate = "impersonate";
        /// <summary>Give a user permissions beyond their roles.</summary>
        public const string Grant = "grant";
    }
}
