using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Extensions;
using EchoPhase.Interfaces;
using EchoPhase.Services.Bitmasks;
using EchoPhase.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EchoPhase.Security
{
    public class JwtTokenService : IJwtTokenService
    {
        private readonly IRoleService _roleService;
        private readonly IRolesBitMaskService _rolesBitmaskService;
        private readonly IAuthService _authService;
        private readonly AesService _aesService;
        private readonly ILogger<JwtTokenService> _logger;
        private readonly BearerSettings _settings;

        private byte[] _key;

        public JwtTokenService(
            IRoleService roleService,
            IRolesBitMaskService rolesBitmaskService,
            IAuthService authService,
            AesService aesService,
            ILogger<JwtTokenService> logger,
            IOptions<AuthenticationSettings> settings,
            IKeysService keysService
        )
        {
            _roleService = roleService;
            _rolesBitmaskService = rolesBitmaskService;
            _authService = authService;
            _aesService = aesService;
            _logger = logger;
            _settings = settings.Value.Schemes.Bearer;

            var result = keysService.GetOrSet(_settings.Key);

            result.OnFailure(err =>
                throw new InvalidOperationException(err.Value));

            if (!result.TryGetValue(out var key))
                throw new InvalidOperationException($"Missing '{_settings.Key}' key.");

            _key = key;
        }

        public virtual async Task<string> GenerateTokenAsync(User user, TimeSpan? lifetime = null)
        {
            if (user is null)
                throw new ArgumentNullException(nameof(user));

            var tokenHandler = new JwtSecurityTokenHandler();

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = await this.GenerateClaimsAsync(user),
                Expires = DateTime.UtcNow + (lifetime ?? TimeSpan.FromMinutes(_settings.LifetimeInMinutes)),
                Issuer = _settings.Issuer,
                Audience = _settings.Audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(_key), SecurityAlgorithms.HmacSha256Signature)
            };

            var securityToken = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(securityToken);

            return tokenString;
        }

        public virtual ClaimsPrincipal? ValidateToken(string token)
        {
            if (token is null)
                throw new ArgumentNullException(nameof(token));

            var tokenHandler = new JwtSecurityTokenHandler();

            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(_key),
                ValidateIssuer = true,
                ValidIssuer = _settings.ValidIssuer,
                ValidateAudience = true,
                ValidAudiences = _settings.ValidAudiences,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            return tokenHandler.ValidateToken(token, tokenValidationParameters, out _);
        }

        private async Task<ClaimsIdentity> GenerateClaimsAsync(User user)
        {
            var claims = new ClaimsIdentity(JwtBearerDefaults.AuthenticationScheme);

            if (user.Id != Guid.Empty)
                claims.AddClaim(new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()));
            if (user.UserName != null)
                claims.AddClaim(new Claim(JwtRegisteredClaimNames.Name, user.UserName));

            var roles = (await _roleService.GetRolesAsync(user)).ToArray();

            if (roles.Length > 0)
            {
                var bitmaskResult = _rolesBitmaskService.Encode(roles);

                bitmaskResult.OnFailure(err =>
                    throw new InvalidOperationException(err.Value));

                if (!bitmaskResult.TryGetValue(out var bitmask))
                    throw new InvalidOperationException("Missing data.");

                var serializationResult = RolesBitMaskService.Serialize(bitmask);

                serializationResult.OnFailure(err =>
                    throw new InvalidOperationException(err.Value));

                if (!serializationResult.TryGetValue(out var serialized))
                    throw new InvalidOperationException("Missing data.");

                claims.AddClaim(new Claim(RolesBitMaskService.ClaimName, serialized));
            }

            return claims;
        }
    }
}

