using System.Security.Claims;
using EchoPhase.DAL.Postgres.Models;

namespace EchoPhase.Identity
{
    /// <summary>
    /// Interface for managing user roles and role-based authorization.
    /// </summary>
    public interface IRoleService
    {
        /// <summary>
        /// Creates a role asynchronously if it does not already exist.
        /// </summary>
        /// <param name="roleName">The name of the role to create.</param>
        Task CreateRoleAsync(string roleName);

        /// <summary>
        /// Adds the specified user to multiple roles asynchronously.
        /// </summary>
        /// <param name="user">The user to add roles to.</param>
        /// <param name="roleNames">The roles to add the user to.</param>
        Task AddToRolesAsync(User user, params IEnumerable<string> roleNames);

        /// <summary>
        /// Adds the specified user to a single role asynchronously.
        /// </summary>
        /// <param name="user">The user to add to the role.</param>
        /// <param name="roleName">The role name.</param>
        Task AddToRoleAsync(User user, string roleName);

        /// <summary>
        /// Removes the specified user from multiple roles asynchronously.
        /// </summary>
        /// <param name="user">The user to remove from roles.</param>
        /// <param name="roleNames">One or more collections of role names.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="Exception">Thrown if removing the user from the role fails.</exception>
        Task RemoveFromRolesAsync(User user, params IEnumerable<string>[] roleNames);

        /// <summary>
        /// Removes the specified user from the given role asynchronously.
        /// </summary>
        /// <param name="user">The user to remove from the role.</param>
        /// <param name="roleName">The name of the role.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="Exception">Thrown if removing the user from the role fails.</exception>
        Task RemoveFromRoleAsync(User user, string roleName);

        /// <summary>
        /// Checks if the specified user is in all the given roles asynchronously.
        /// </summary>
        /// <param name="user">The user to check.</param>
        /// <param name="roleNames">The roles to check membership against.</param>
        /// <returns>True if the user is in all roles, otherwise false.</returns>
        Task<bool> IsInRolesAsync(User user, params IEnumerable<string> roleNames);

        /// <summary>
        /// Checks if the specified user is in a given role asynchronously.
        /// </summary>
        /// <param name="user">The user to check.</param>
        /// <param name="roleName">The role to check.</param>
        /// <returns>True if the user is in the role, otherwise false.</returns>
        Task<bool> IsInRoleAsync(User user, string roleName);

        /// <summary>
        /// Checks if the ClaimsPrincipal is in all the specified roles synchronously.
        /// </summary>
        /// <param name="userClaims">The claims principal to check.</param>
        /// <param name="roleNames">The roles to check.</param>
        /// <returns>True if in all roles, otherwise false.</returns>
        bool IsInRoles(ClaimsPrincipal userClaims, params IEnumerable<string> roleNames);

        /// <summary>
        /// Checks if the ClaimsPrincipal is in a specified role synchronously.
        /// </summary>
        /// <param name="userClaims">The claims principal to check.</param>
        /// <param name="roleName">The role to check.</param>
        /// <returns>True if in the role, otherwise false.</returns>
        bool IsInRole(ClaimsPrincipal userClaims, string roleName);

        /// <summary>
        /// Retrieves the roles assigned to a user asynchronously.
        /// </summary>
        /// <param name="user">The user whose roles to get.</param>
        /// <returns>An enumerable of role names.</returns>
        Task<IEnumerable<string>> GetRolesAsync(User user);

        /// <summary>
        /// Retrieves the roles assigned to a ClaimsPrincipal synchronously.
        /// </summary>
        /// <param name="userClaims">The claims principal.</param>
        /// <returns>An enumerable of role names.</returns>
        IEnumerable<string> GetRoles(ClaimsPrincipal userClaims);

        /// <summary>
        /// Retrieves all users assigned to a specific role asynchronously.
        /// </summary>
        /// <param name="role">The role name.</param>
        /// <returns>An enumerable of users in the role.</returns>
        Task<IEnumerable<User>> GetUsersInRoleAsync(string role);

        /// <summary>
        /// Retrieves all users assigned to any of the specified roles asynchronously.
        /// </summary>
        /// <param name="roles">The roles to check.</param>
        /// <returns>An enumerable of users in the specified roles.</returns>
        Task<IEnumerable<User>> GetUsersInRolesAsync(IEnumerable<string> roles);
    }
}
