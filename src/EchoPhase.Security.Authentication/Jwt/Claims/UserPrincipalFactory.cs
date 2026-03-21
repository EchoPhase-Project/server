// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Security.Authentication.Jwt.Claims.Providers;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace EchoPhase.Security.Authentication.Jwt.Claims
{
    public class UserPrincipalFactory : IUserPrincipalFactory
    {
        private readonly IEnumerable<IClaimsProvider> _providers;

        public UserPrincipalFactory(IEnumerable<IClaimsProvider> providers)
        {
            _providers = providers;
        }

        public async Task<ClaimsPrincipal> CreateAsync(User user, ClaimsEnrichmentContext? context = null)
        {
            context ??= new ClaimsEnrichmentContext { User = user };

            var identity = new ClaimsIdentity(
                authenticationType: JwtBearerDefaults.AuthenticationScheme,
                nameType: JwtRegisteredClaimNames.Name.ToString());

            if (user.Id != Guid.Empty)
                identity.AddClaim(new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()));
            if (user.UserName != null)
                identity.AddClaim(new Claim(JwtRegisteredClaimNames.Name, user.UserName));
            if (user.SecurityStamp != null)
                identity.AddClaim(new Claim(JwtRegisteredClaimNames.Jti, user.SecurityStamp));

            foreach (var provider in _providers)
                await provider.EnrichAsync(identity, context);

            return new ClaimsPrincipal(identity);
        }
    }
}
