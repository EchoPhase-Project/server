using EchoPhase.Models;
using System.Security.Claims;

namespace EchoPhase.Interfaces
{
	public interface IJwtTokenService
	{
		Task<string> GenerateTokenAsync(User user);
		ClaimsPrincipal? ValidateToken(string token);
		void RevokeToken(ClaimsPrincipal user, string token);
		void ExtendToken(ClaimsPrincipal user, string token);
	}
}
