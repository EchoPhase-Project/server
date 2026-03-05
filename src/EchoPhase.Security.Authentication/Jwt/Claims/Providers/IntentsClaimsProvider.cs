using EchoPhase.Security.BitMasks;
using System.Security.Claims;
using EchoPhase.Types.Result.Extensions;

namespace EchoPhase.Security.Authentication.Jwt.Claims.Providers
{
    public class IntentsClaimsProvider : IClaimsProvider
    {
        private readonly IntentsBitMask _bitmask;

        public IntentsClaimsProvider(IntentsBitMask bitmask)
            => _bitmask = bitmask;

        public Task EnrichAsync(ClaimsIdentity identity, ClaimsEnrichmentContext context)
        {
            if (context.RequestedIntents.Count == 0) return Task.CompletedTask;

            var bitmask = _bitmask.Encode(context.RequestedIntents.ToArray())
                .GetValueOrThrow();

            var serialized = _bitmask.Serialize(bitmask)
                .GetValueOrThrow();

            identity.AddClaim(new Claim(_bitmask.ClaimName, serialized));
            return Task.CompletedTask;
        }
    }
}
