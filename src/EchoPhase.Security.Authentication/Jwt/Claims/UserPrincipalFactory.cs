using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using EchoPhase.DAL.Postgres.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace EchoPhase.Security.Authentication.Jwt.Claims
{
    public class UserPrincipalFactory : IUserPrincipalFactory
    {
        private readonly IClaimsProviderRegistry _registry;

        public UserPrincipalFactory(IClaimsProviderRegistry registry)
        {
            _registry = registry;
        }

        public async Task<ClaimsPrincipal> CreateAsync(User user, ClaimsEnrichmentContext? context = null)
        {
            context ??= new ClaimsEnrichmentContext { User = user };

            var identity = new ClaimsIdentity(
                authenticationType: JwtBearerDefaults.AuthenticationScheme,
                nameType:           JwtRegisteredClaimNames.Name.ToString());

            if (user.Id != Guid.Empty)
                identity.AddClaim(new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()));
            if (user.UserName != null)
                identity.AddClaim(new Claim(JwtRegisteredClaimNames.Name, user.UserName));
            if (user.SecurityStamp != null)
                identity.AddClaim(new Claim(JwtRegisteredClaimNames.Jti, user.SecurityStamp));

            foreach (var provider in _registry.GetProviders())
                await provider.EnrichAsync(identity, context);

            return new ClaimsPrincipal(identity);
        }
    }
}
