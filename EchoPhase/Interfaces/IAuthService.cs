using System.Security.Claims;
using EchoPhase.DAL.Postgres.Models;
using Microsoft.AspNetCore.Identity;

namespace EchoPhase.Interfaces
{
    public interface IAuthService
    {
        Task<IdentityResult> CreateUserAsync(string name, string username, string password, params string[] roles);
        Task<IdentityResult> DeleteUserAsync(User user);
        Task<SignInResult> AuthenticateAsync(string username, string password);
        Task LogoutAsync();
        Task UnlockUserAsync(User user);
        Task ResetAccessFailedCountAsync(User user);
        bool IsAuthenticated(ClaimsPrincipal user);
        Task<bool> IsInPolicyAsync(ClaimsPrincipal userPrincipal, string policyName);
        Task<bool> IsInPoliciesAsync(ClaimsPrincipal userPrincipal, IEnumerable<string> policiesName);
    }
}
