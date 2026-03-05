using EchoPhase.Security.Authentication.Jwt.Claims.Providers;

namespace EchoPhase.Security.Authentication.Jwt.Claims
{
    public interface IClaimsProviderRegistry
    {
        IReadOnlyList<IClaimsProvider> GetProviders();
    }
}
