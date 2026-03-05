using EchoPhase.Identity;
using EchoPhase.Security.BitMasks;
using System.Security.Claims;
using EchoPhase.Types.Result.Extensions;

namespace EchoPhase.Security.Authentication.Jwt.Claims.Providers
{
    public class RolesClaimsProvider : IClaimsProvider
    {
        private readonly IRoleService _roleService;
        private readonly RolesBitMask _bitmask;

        public RolesClaimsProvider(IRoleService roleService, RolesBitMask bitmask)
        {
            _roleService = roleService;
            _bitmask = bitmask;
        }

        public async Task EnrichAsync(ClaimsIdentity identity, ClaimsEnrichmentContext context)
        {
            var roles = (await _roleService.GetRolesAsync(context.User)).ToArray();
            if (roles.Length == 0) return;

            var bitmask = _bitmask.Encode(roles)
                .GetValueOrThrow();

            var serialized = _bitmask.Serialize(bitmask)
                .GetValueOrThrow();

            identity.AddClaim(new Claim(_bitmask.ClaimName, serialized));
        }
    }
}
