// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Security.BitMasks.Constants;

namespace EchoPhase.Security.BitMasks
{
    /// <summary>
    /// Intents are a subscription filter the client chooses; whether it may
    /// subscribe is decided by permissions. Each intent requires one permission,
    /// so a narrowed API client only receives events about what it may read.
    /// </summary>
    public static class IntentRequirements
    {
        public static readonly IReadOnlyDictionary<string, (string Resource, string Permission)> Map =
            new Dictionary<string, (string, string)>(StringComparer.Ordinal)
            {
                [Intents.Login] = (Resources.Sessions, Permissions.Read),
                [Intents.Connections] = (Resources.Connections, Permissions.Read),
                [Intents.Impersonation] = (Resources.Sessions, Permissions.Read),
                [Intents.Blocks] = (Resources.Scripts, Permissions.Read),
            };

        /// <summary>
        /// Requested intents the permissions do not cover. An intent without a
        /// mapping is never allowed, so a new intent is private until mapped.
        /// </summary>
        public static IReadOnlyList<string> Disallowed(
            IEnumerable<string> intents,
            IReadOnlyDictionary<string, string[]> permissions)
            => intents
                .Where(intent => !Map.TryGetValue(intent, out var required)
                    || !permissions.TryGetValue(required.Resource, out var actions)
                    || !actions.Contains(required.Permission, StringComparer.Ordinal))
                .ToList();
    }
}
