using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EchoPhase.DAL.Postgres;
using EchoPhase.Interfaces;
using EchoPhase.Models;
using EchoPhase.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EchoPhase.Services.Security
{
    public class JwtTokenService : IJwtTokenService
    {
        private readonly PostgresContext _dbContext;
        private readonly IRoleService _roleService;
        private readonly IAuthService _authService;
        private readonly AesService _aesService;
        private readonly ILogger<JwtTokenService> _logger;
        private readonly JwtSettings _settings;

        public JwtTokenService(
            PostgresContext dbContext,
            IRoleService roleService,
            IAuthService authService,
            AesService aesService,
            ILogger<JwtTokenService> logger,
            IOptions<JwtSettings> settings
        )
        {
            _dbContext = dbContext;
            _roleService = roleService;
            _authService = authService;
            _aesService = aesService;
            _logger = logger;
            _settings = settings.Value;
        }

        public virtual async Task<string> GenerateTokenAsync(User user)
        {
            if (user is null)
                throw new ArgumentNullException(nameof(user));

            var checkToken = _dbContext.JwtTokens
                .FirstOrDefault(jt => jt.UserId == user.Id && jt.ExpiryDate > DateTime.UtcNow);

            if (checkToken != null)
                return checkToken.Token;

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_settings.Secret);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = await this.GenerateClaimsAsync(user),
                Expires = DateTime.UtcNow.AddMinutes(_settings.ExpirationInMinutes),
                Issuer = _settings.Issuer,
                Audience = _settings.Audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            _dbContext.JwtTokens.Add(new JwtToken
            {
                UserId = user.Id,
                Token = _aesService.Encrypt(tokenString),
                ExpiryDate = tokenDescriptor.Expires.Value
            });
            _dbContext.SaveChanges();

            return tokenString;
        }

        public virtual ClaimsPrincipal? ValidateToken(string token)
        {
            if (token is null)
                throw new ArgumentNullException(nameof(token));

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_settings.Secret);

            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _settings.Issuer,
                ValidateAudience = true,
                ValidAudience = _settings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            try
            {
                var invalidatedToken = _dbContext.JwtTokens
                    .FirstOrDefault(it => _aesService.Decrypt(it.Token) == token && it.ExpiryDate < DateTime.UtcNow);

                if (invalidatedToken != null)
                    _logger?.LogWarning($"Token has expired.");

                return tokenHandler.ValidateToken(token, tokenValidationParameters, out _);
            }
            catch (SecurityTokenException ex)
            {
                _logger?.LogWarning(ex, "Token validation failed.");
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "An unexpected error occurred during token validation.");
                throw;
            }
        }

        public virtual void RevokeToken(ClaimsPrincipal user, string token)
        {
            var invalidatedToken = GetToken(token);

            //if (invalidatedToken.UserId != user.GetUserId() && await _authService.IsInPolicyAsync(user))

            invalidatedToken.ExpiryDate = DateTime.UtcNow;
            _dbContext.SaveChanges();

            _logger.LogInformation($"Token {token} is revoked.");
        }

        public virtual void ExtendToken(ClaimsPrincipal user, string token)
        {
            var invalidatedToken = GetToken(token);

            invalidatedToken.ExpiryDate = DateTime.UtcNow.AddMinutes(_settings.ExpirationInMinutes);
            _dbContext.SaveChanges();

            _logger.LogInformation($"Token {token} is extended.");
        }

        private async Task<ClaimsIdentity> GenerateClaimsAsync(User user)
        {
            var claims = new ClaimsIdentity(JwtBearerDefaults.AuthenticationScheme);

            if (user.Id != Guid.Empty)
                claims.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
            if (user.UserName != null)
                claims.AddClaim(new Claim(ClaimTypes.Name, user.UserName));

            var roles = await _roleService.GetRolesAsync(user);
            foreach (var role in roles)
                claims.AddClaim(new Claim(ClaimTypes.Role, role));
            claims.AddClaim(new Claim(ClaimTypes.Actor, "Hello"));

            return claims;
        }

        private JwtToken GetToken(string token)
        {
            var invalidatedToken = _dbContext.JwtTokens
                .FirstOrDefault(it => _aesService.Decrypt(it.Token) == token);

            if (invalidatedToken is null)
                throw new InvalidOperationException("Token does not exist.");

            return invalidatedToken;
        }
    }
}

