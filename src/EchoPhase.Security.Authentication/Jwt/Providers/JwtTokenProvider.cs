// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EchoPhase.Configuration.Authentication;
using EchoPhase.Configuration.Authentication.Bearer;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Security.Authentication.Jwt.Claims;
using EchoPhase.Security.Cryptography.Vaults;
using EchoPhase.Types.Result.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EchoPhase.Security.Authentication.Jwt.Providers
{
    public class JwtTokenProvider : IJwtTokenProvider
    {
        private readonly ILogger<JwtTokenProvider> _logger;
        private readonly BearerOptions _settings;
        private readonly UserRepository _userRepository;
        private readonly IUserPrincipalFactory _principalFactory;
        private readonly byte[] _key;

        public JwtTokenProvider(
            ILogger<JwtTokenProvider> logger,
            IOptions<AuthenticationOptions> settings,
            IKeyVault keyVault,
            UserRepository userRepository,
            IUserPrincipalFactory principalFactory
        )
        {
            _logger = logger;
            _settings = settings.Value.Bearer;
            _userRepository = userRepository;
            _principalFactory = principalFactory;

            var result = keyVault.GetOrSet(_settings.Key);
            result.OnFailure(err => throw new InvalidOperationException(err.Value));

            if (!result.TryGetValue(out var key))
                throw new InvalidOperationException($"Missing '{_settings.Key}' key.");

            _key = key;
        }

        public virtual async Task<string> GenerateTokenAsync(
            User user,
            TimeSpan? lifetime = null,
            ClaimsEnrichmentContext? context = null)
        {
            if (user is null) throw new ArgumentNullException(nameof(user));

            var principal = await _principalFactory.CreateAsync(user, context);
            var identity = (ClaimsIdentity)principal.Identity!;

            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = identity,
                Expires = DateTime.UtcNow + (lifetime ?? _settings.Lifetime),
                Issuer = _settings.Issuer,
                Audience = _settings.Audience,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(_key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
        }

        public virtual async Task<ClaimsPrincipal?> ValidateTokenAsync(string token)
        {
            if (token is null)
                throw new ArgumentNullException(nameof(token));

            var tokenHandler = new JwtSecurityTokenHandler();
            tokenHandler.InboundClaimTypeMap.Clear();
            var tokenValidationParameters = new TokenValidationParameters
            {
                AuthenticationType = JwtBearerDefaults.AuthenticationScheme,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(_key),
                ValidateIssuer = true,
                ValidIssuer = _settings.ValidIssuer,
                ValidateAudience = true,
                ValidAudiences = _settings.ValidAudiences,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            ClaimsPrincipal claims;
            try
            {
                claims = tokenHandler.ValidateToken(token, tokenValidationParameters, out _);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("JWT signature/lifetime validation failed: {Message}", ex.Message);
                return null;
            }

            var sub = claims.FindFirstValue(JwtRegisteredClaimNames.Sub);
            if (sub is null || !Guid.TryParse(sub, out var userId))
            {
                _logger.LogWarning("JWT missing or invalid 'sub' claim");
                return null;
            }

            var user = _userRepository.Query()
                .WithIds(userId)
                .FirstOrDefault();

            if (user is null)
            {
                _logger.LogWarning("User {UserId} not found in DB", userId);
                return null;
            }

            var stamp = claims.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

            if (stamp != user.SecurityStamp)
            {
                _logger.LogWarning("SecurityStamp mismatch for user {UserId}", userId);
                throw new SecurityTokenExpiredException("Security stamp mismatch.");
            }

            return claims;
        }
    }
}
