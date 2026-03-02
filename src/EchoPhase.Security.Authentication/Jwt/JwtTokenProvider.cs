using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EchoPhase.Configuration.Authentication;
using EchoPhase.Configuration.Authentication.Bearer;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Identity;
using EchoPhase.Security.BitMasks;
using EchoPhase.Security.Cryptography;
using EchoPhase.Security.Cryptography.Vaults;
using EchoPhase.Types.Result.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EchoPhase.Security.Authentication.Jwt
{
    public class JwtTokenProvider : IJwtTokenProvider
    {
        private readonly IRoleService _roleService;
        private readonly IRolesBitMask _rolesBitmask;
        private readonly AesGcm _aes;
        private readonly ILogger<JwtTokenProvider> _logger;
        private readonly BearerOptions _settings;
        private readonly IUserService _userService;

        private byte[] _key;

        public JwtTokenProvider(
            IRoleService roleService,
            IRolesBitMask rolesBitmask,
            AesGcm aes,
            ILogger<JwtTokenProvider> logger,
            IOptions<AuthenticationOptions> settings,
            IKeyVault keyVault,
            IUserService userService
        )
        {
            _roleService = roleService;
            _rolesBitmask = rolesBitmask;
            _aes = aes;
            _logger = logger;
            _settings = settings.Value.Bearer;
            _userService = userService;

            var result = keyVault.GetOrSet(_settings.Key);

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
                Expires = DateTime.UtcNow + (lifetime ?? _settings.Lifetime),
                Issuer = _settings.Issuer,
                Audience = _settings.Audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(_key), SecurityAlgorithms.HmacSha256Signature)
            };

            var securityToken = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(securityToken);

            return tokenString;
        }

        public virtual async Task<ClaimsPrincipal?> ValidateTokenAsync(string token)
        {
            if (token is null)
                throw new ArgumentNullException(nameof(token));

            var tokenHandler = new JwtSecurityTokenHandler();

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

            var claims = tokenHandler.ValidateToken(token, tokenValidationParameters, out _);

            var user = await _userService.GetAsync(claims);

            if (user is null)
                throw new InvalidOperationException("Invalid user.");

            var stamp = claims.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

            if (stamp != user.SecurityStamp)
                throw new InvalidOperationException("Invalid token.");

            return claims;
        }

        private async Task<ClaimsIdentity> GenerateClaimsAsync(User user)
        {
            var claims = new ClaimsIdentity(JwtBearerDefaults.AuthenticationScheme);

            if (user.Id != Guid.Empty)
                claims.AddClaim(new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()));
            if (user.UserName != null)
                claims.AddClaim(new Claim(JwtRegisteredClaimNames.Name, user.UserName));
            if (user.SecurityStamp != null)
                claims.AddClaim(new Claim(JwtRegisteredClaimNames.Jti, user.SecurityStamp));

            var roles = (await _roleService.GetRolesAsync(user)).ToArray();

            if (roles.Length > 0)
            {
                var bitmaskResult = _rolesBitmask.Encode(roles);

                bitmaskResult.OnFailure(err =>
                    throw new InvalidOperationException(err.Value));

                if (!bitmaskResult.TryGetValue(out var bitmask))
                    throw new InvalidOperationException("Missing data.");

                var serializationResult = RolesBitMask.Serialize(bitmask);

                serializationResult.OnFailure(err =>
                    throw new InvalidOperationException(err.Value));

                if (!serializationResult.TryGetValue(out var serialized))
                    throw new InvalidOperationException("Missing data.");

                claims.AddClaim(new Claim(RolesBitMask.ClaimName, serialized));
            }

            return claims;
        }
    }
}

