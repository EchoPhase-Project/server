using System.Security.Claims;

namespace EchoPhase.Security.Authentication.Jwt.Claims.Providers
{
    public interface IClaimsProvider
    {
        Task EnrichAsync(ClaimsIdentity identity, ClaimsEnrichmentContext context);
    }
}
