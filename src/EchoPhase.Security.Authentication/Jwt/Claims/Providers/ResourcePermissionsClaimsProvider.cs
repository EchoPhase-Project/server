using EchoPhase.Security.BitMasks;
using System.Security.Claims;
using EchoPhase.Types.Result.Extensions;

namespace EchoPhase.Security.Authentication.Jwt.Claims.Providers
{
    public class ResourcePermissionsClaimsProvider : IClaimsProvider
    {
        private readonly ResourcePermissionsBitMask _bitmask;

        public ResourcePermissionsClaimsProvider(ResourcePermissionsBitMask bitmask)
            => _bitmask = bitmask;

        public Task EnrichAsync(ClaimsIdentity identity, ClaimsEnrichmentContext context)
        {
            if (context.RequestedPermissions.Count == 0) return Task.CompletedTask;

            var encoded = _bitmask.Encode(context.RequestedPermissions)
                .GetValueOrThrow();

            var serialized = _bitmask.Serialize(encoded)
                .GetValueOrThrow();

            identity.AddClaim(new Claim(_bitmask.ClaimName, serialized));
            return Task.CompletedTask;
        }
    }
}
