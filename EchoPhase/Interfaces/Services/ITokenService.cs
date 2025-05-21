using EchoPhase.Models;
using System.Security.Claims;

namespace EchoPhase.Interfaces
{
	public interface ITokenService
	{
		Task<string> GenerateTokenAsync(User user);
		ClaimsPrincipal? ValidateToken(string token);
		void RevokeToken(string token);
		void ExtendToken(string token);
	}
}
