using System.Security.Claims;
using EchoPhase.Security.BitMasks;
using EchoPhase.Types.Result.Extensions;

namespace EchoPhase.Security.Authentication.Jwt.Claims.Providers
{
    public class ScopesClaimsProvider : IClaimsProvider
    {
        private readonly ScopesBitMask _bitmask;

        public ScopesClaimsProvider(ScopesBitMask bitmask)
            => _bitmask = bitmask;

        public Task EnrichAsync(ClaimsIdentity identity, ClaimsEnrichmentContext context)
        {
            if (context.RequestedScopes.Count == 0) return Task.CompletedTask;

            var bitmask = _bitmask.Encode(context.RequestedScopes.ToArray())
                .GetValueOrThrow();

            var serialized = _bitmask.Serialize(bitmask)
                .GetValueOrThrow();

            identity.AddClaim(new Claim(_bitmask.ClaimName, serialized));
            return Task.CompletedTask;
        }
    }
}
