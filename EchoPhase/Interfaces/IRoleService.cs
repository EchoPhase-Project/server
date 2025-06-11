using System.Security.Claims;

using EchoPhase.Models;

namespace EchoPhase.Interfaces
{
    public interface IRoleService
    {
        public Task CreateRoleAsync(string roleName);
        public Task AddToRolesAsync(User user, params IEnumerable<string> roleNames);
        public Task AddToRoleAsync(User user, string roleName);
        public Task<bool> IsInRolesAsync(User user, params IEnumerable<string> roleNames);
        public Task<bool> IsInRoleAsync(User user, string roleName);
        public bool IsInRoles(ClaimsPrincipal userClaims, params IEnumerable<string> roleNames);
        public bool IsInRole(ClaimsPrincipal userClaims, string roleName);
        public Task<IEnumerable<string>> GetRolesAsync(User user);
        public IEnumerable<string> GetRoles(ClaimsPrincipal userClaims);
        public Task<IEnumerable<User>> GetUsersInRoleAsync(string role);
        public Task<IEnumerable<User>> GetUsersInRolesAsync(IEnumerable<string> roles);
    }
}
