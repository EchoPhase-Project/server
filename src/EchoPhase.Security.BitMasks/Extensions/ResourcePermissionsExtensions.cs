// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Security.Claims;
using EchoPhase.Types.Result.Extensions;

namespace EchoPhase.Security.BitMasks.Extensions
{
    public static class ResourcePermissionsExtensions
    {
        /// <summary>
        /// The effective permissions carried by a token; empty when it has none.
        /// </summary>
        public static IReadOnlyDictionary<string, string[]> GetPermissions(
            this ResourcePermissionsBitMask mask, ClaimsPrincipal principal)
        {
            var claim = principal.FindFirst(mask.ClaimName)?.Value;
            if (claim is null)
                return new Dictionary<string, string[]>();

            if (!mask.Deserialize(claim).TryGetValue(out var bits))
                return new Dictionary<string, string[]>();

            return mask.Decode(bits).TryGetValue(out var decoded)
                ? decoded
                : new Dictionary<string, string[]>();
        }
    }
}
