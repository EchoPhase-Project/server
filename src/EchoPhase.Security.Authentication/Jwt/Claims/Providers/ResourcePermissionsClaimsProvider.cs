// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Security.Claims;
using EchoPhase.Identity;
using EchoPhase.Security.BitMasks;
using EchoPhase.Types.Result.Extensions;

namespace EchoPhase.Security.Authentication.Jwt.Claims.Providers
{
    /// <summary>
    /// Writes the token's effective permissions: the user's current ceiling
    /// (roles plus active grants), narrowed to what was requested (API
    /// clients). Roles and grants themselves are not put in the token.
    /// </summary>
    public class ResourcePermissionsClaimsProvider : IClaimsProvider
    {
        private readonly ResourcePermissionsBitMask _bitmask;
        private readonly IUserPermissionService _permissions;

        public ResourcePermissionsClaimsProvider(ResourcePermissionsBitMask bitmask, IUserPermissionService permissions)
        {
            _bitmask = bitmask;
            _permissions = permissions;
        }

        public async Task EnrichAsync(ClaimsIdentity identity, ClaimsEnrichmentContext context)
        {
            var ceiling = await _permissions.GetCeilingAsync(context.User);

            var effective = context.RequestedPermissions is null
                ? ceiling
                : RolePermissions.Intersect(ceiling, context.RequestedPermissions);

            if (effective.Count == 0) return;

            var encoded = _bitmask.Encode(effective)
                .GetValueOrThrow();

            var serialized = _bitmask.Serialize(encoded)
                .GetValueOrThrow();

            identity.AddClaim(new Claim(_bitmask.ClaimName, serialized));
        }
    }
}
