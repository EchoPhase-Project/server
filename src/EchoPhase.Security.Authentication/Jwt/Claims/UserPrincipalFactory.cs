// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Security.Authentication.Jwt.Claims.Providers;
using EchoPhase.Types.Claims;
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
            if (context.ClientId is { } clientId)
                identity.AddClaim(new Claim(EchoPhaseClaimTypes.ClientId, clientId.ToString()));
            if (context.ActorId is { } actorId && context.ImpersonationId is { } impersonationId)
            {
                identity.AddClaim(new Claim(
                    EchoPhaseClaimTypes.Act,
                    JsonSerializer.Serialize(new { sub = actorId.ToString() }),
                    JsonClaimValueTypes.Json));
                identity.AddClaim(new Claim(EchoPhaseClaimTypes.ImpersonationId, impersonationId.ToString()));
            }

            foreach (var provider in _providers)
                await provider.EnrichAsync(identity, context);

            return new ClaimsPrincipal(identity);
        }
    }
}
