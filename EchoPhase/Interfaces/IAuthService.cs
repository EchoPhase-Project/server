using System.Security.Claims;

using Microsoft.AspNetCore.Identity;

namespace EchoPhase.Interfaces
{
	public interface IAuthService
	{
		public Task<IdentityResult> CreateUserAsync(string name, string username, string password);
		public Task<SignInResult> AuthenticateAsync(string username, string password);
		public Task LogoutAsync();
		public bool IsAuthenticated(ClaimsPrincipal user);
		public Task<bool> IsInPolicyAsync(ClaimsPrincipal userPrincipal, string policyName);
		public Task<bool> IsInPoliciesAsync(ClaimsPrincipal userPrincipal, IEnumerable<string> policiesName);
	}
}
