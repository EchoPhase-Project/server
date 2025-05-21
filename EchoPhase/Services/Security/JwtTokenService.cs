using System.Text;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using EchoPhase.DAL.Postgres;
using EchoPhase.Models;
using EchoPhase.Interfaces;
using EchoPhase.Configurations.Models;

namespace EchoPhase.Services.Security
{
	public class JwtTokenService : ITokenService
	{
		private readonly PostgresContext _dbContext;
		private readonly RoleService _roleService;
		private readonly ILogger<JwtTokenService> _logger;
		private readonly JwtSettings _settings;

		public JwtTokenService(
			PostgresContext dbContext, 
			RoleService roleService, 
			ILogger<JwtTokenService> logger,
			IOptions<JwtSettings> settings
		)
		{
			_dbContext = dbContext;
			_roleService = roleService;
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
			var key = Encoding.ASCII.GetBytes(_settings.SecretKey);

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
				Token = tokenString,
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
			var key = Encoding.ASCII.GetBytes(_settings.SecretKey);

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
					.FirstOrDefault(it => it.Token == token && it.ExpiryDate < DateTime.UtcNow);

				if (invalidatedToken is null)
					return tokenHandler.ValidateToken(token, tokenValidationParameters, out _);

				_logger?.LogWarning($"Token has expired.");
			}
			catch (SecurityTokenException ex)
			{
				_logger?.LogWarning(ex, "Token validation failed.");
			}
			catch (Exception ex)
			{
				_logger?.LogError(ex, "An unexpected error occurred during token validation.");
			}

			return default;
		}

		private async Task<ClaimsIdentity> GenerateClaimsAsync(User user)
		{
			if (user is null)
				throw new ArgumentNullException(nameof(user));

			var claims = new ClaimsIdentity();
			if (user.Id != Guid.Empty)
				claims.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
			if (user.UserName != null)
				claims.AddClaim(new Claim(ClaimTypes.Name, user.UserName));

			var roles = await _roleService.GetRolesAsync(user);
			foreach (var role in roles)
				claims.AddClaim(new Claim(ClaimTypes.Role, role));

			return claims;
		}

		public virtual void RevokeToken(string token)
		{
			if (token is null)
				throw new ArgumentNullException(nameof(token));

			var invalidatedToken = _dbContext.JwtTokens
				.FirstOrDefault(it => it.Token == token);
			
			if (invalidatedToken is null)
			{
				_logger.LogError("Token does not exist.");
				return;
			}

			invalidatedToken.ExpiryDate = DateTime.UtcNow;
			_dbContext.SaveChanges();

			_logger.LogInformation($"Token {token} is revoked.");
		}

		public virtual void ExtendToken(string token)
		{
			var invalidatedToken = _dbContext.JwtTokens
				.FirstOrDefault(it => it.Token == token);
			
			if (invalidatedToken is null)
			{
				_logger.LogError("Token does not exist.");
				return;
			}

			invalidatedToken.ExpiryDate = DateTime.UtcNow.AddMinutes(_settings.ExpirationInMinutes);
			_dbContext.SaveChanges();

			_logger.LogInformation($"Token {token} is extended.");
		}
	}
}

