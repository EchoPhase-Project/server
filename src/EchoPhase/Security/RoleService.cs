using System.Security.Claims;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace EchoPhase.Security
{
    /// <summary>
    /// Provides functionality to manage and retrieve user roles.
    /// </summary>
    public class RoleService : IRoleService
    {
        private readonly RoleManager<UserRole> _roleManager;
        private readonly UserManager<User> _userManager;
        private readonly UserRepository _userRepository;

        public const string RoleClaim = "role";

        /// <summary>
        /// Initializes a new instance of the <see cref="RoleService"/> class.
        /// </summary>
        /// <param name="roleManager">The RoleManager to manage user roles.</param>
        /// <param name="userManager">The UserManager to manage users.</param>
        /// <param name="userRepository">The repository for user data access.</param>
        public RoleService(
            RoleManager<UserRole> roleManager,
            UserManager<User> userManager,
            UserRepository userRepository
        )
        {
            _roleManager = roleManager;
            _userManager = userManager;
            _userRepository = userRepository;
        }

        /// <summary>
        /// Creates a new role asynchronously if it does not already exist.
        /// </summary>
        /// <param name="roleName">The name of the role to create.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="Exception">Thrown when role creation fails.</exception>
        public async Task CreateRoleAsync(string roleName)
        {
            if (await _roleManager.RoleExistsAsync(roleName))
                return;

            var role = new UserRole { Name = roleName };
            var result = await _roleManager.CreateAsync(role);
            if (!result.Succeeded)
                throw new Exception($"Failed to create role {roleName}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        /// <summary>
        /// Adds the specified user to multiple roles asynchronously.
        /// </summary>
        /// <param name="user">The user to add to roles.</param>
        /// <param name="roleNames">One or more collections of role names.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task AddToRolesAsync(User user, params IEnumerable<string> roleNames)
        {
            foreach (var roleName in roleNames)
                await AddToRoleAsync(user, roleName);
        }

        /// <summary>
        /// Adds the specified user to the given role asynchronously.
        /// Ensures the role exists by creating it if it does not.
        /// </summary>
        /// <param name="user">The user to add to the role.</param>
        /// <param name="roleName">The name of the role.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="Exception">Thrown if adding the user to the role fails.</exception>
        public async Task AddToRoleAsync(User user, string roleName)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                await CreateRoleAsync(roleName);
            }

            if (await IsInRoleAsync(user, roleName))
                return;

            var result = await _userManager.AddToRoleAsync(user, roleName);
            if (!result.Succeeded)
                throw new Exception($"Failed to add to role {roleName}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        /// <summary>
        /// Removes the specified user from multiple roles asynchronously.
        /// </summary>
        /// <param name="user">The user to remove from roles.</param>
        /// <param name="roleNames">One or more collections of role names.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task RemoveFromRolesAsync(User user, params IEnumerable<string>[] roleNames)
        {
            foreach (var roles in roleNames)
            {
                foreach (var roleName in roles)
                {
                    await RemoveFromRoleAsync(user, roleName);
                }
            }
        }

        /// <summary>
        /// Removes the specified user from the given role asynchronously.
        /// </summary>
        /// <param name="user">The user to remove from the role.</param>
        /// <param name="roleName">The name of the role.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="Exception">Thrown if removing the user from the role fails.</exception>
        public async Task RemoveFromRoleAsync(User user, string roleName)
        {
            if (!await IsInRoleAsync(user, roleName))
                return;

            var result = await _userManager.RemoveFromRoleAsync(user, roleName);
            if (!result.Succeeded)
                throw new Exception($"Failed to remove from role {roleName}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        /// <summary>
        /// Checks if the specified user is in all of the given roles asynchronously.
        /// </summary>
        /// <param name="user">The user to check roles for.</param>
        /// <param name="roleNames">One or more collections of role names to check.</param>
        /// <returns>A task that represents the asynchronous operation. The task result is true if the user is in all specified roles; otherwise, false.</returns>
        public async Task<bool> IsInRolesAsync(User user, params IEnumerable<string> roleNames)
        {
            foreach (var roleName in roleNames)
                if (!(await IsInRoleAsync(user, roleName)))
                    return false;
            return true;
        }

        /// <summary>
        /// Checks asynchronously if the specified user is in a given role.
        /// </summary>
        /// <param name="user">The user to check.</param>
        /// <param name="roleName">The name of the role to check for.</param>
        /// <returns>A task representing the asynchronous operation. The task result is true if the user is in the role; otherwise, false.</returns>
        public async Task<bool> IsInRoleAsync(User user, string roleName) =>
            await _userManager.IsInRoleAsync(user, roleName);

        /// <summary>
        /// Checks if the ClaimsPrincipal has all specified roles.
        /// </summary>
        /// <param name="userClaims">The ClaimsPrincipal representing the user.</param>
        /// <param name="roleNames">One or more collections of role names to check.</param>
        /// <returns>True if the user has all the specified roles; otherwise, false.</returns>
        public bool IsInRoles(ClaimsPrincipal userClaims, params IEnumerable<string> roleNames)
        {
            foreach (var roleName in roleNames)
                if (!IsInRole(userClaims, roleName))
                    return false;
            return true;
        }

        /// <summary>
        /// Checks if the ClaimsPrincipal is in the specified role.
        /// </summary>
        /// <param name="userClaims">The ClaimsPrincipal representing the user.</param>
        /// <param name="roleName">The name of the role to check.</param>
        /// <returns>True if the user is in the specified role; otherwise, false.</returns>
        public bool IsInRole(ClaimsPrincipal userClaims, string roleName) =>
            userClaims.IsInRole(roleName);

        /// <summary>
        /// Retrieves the roles for the specified user asynchronously.
        /// </summary>
        /// <param name="user">The user whose roles are to be retrieved.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the collection of role names associated with the user.</returns>
        public async Task<IEnumerable<string>> GetRolesAsync(User user) =>
            await _userManager.GetRolesAsync(user);

        /// <summary>
        /// Retrieves all users who belong to any of the specified roles.
        /// </summary>
        /// <param name="roles">The collection of role names to search for users.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a distinct collection of users belonging to any of the given roles.</returns>
        public async Task<IEnumerable<User>> GetUsersInRolesAsync(IEnumerable<string> roles)
        {
            var users = new HashSet<User>();

            foreach (var role in roles.Distinct())
            {
                var usersInRole = await _userManager.GetUsersInRoleAsync(role);
                foreach (var user in usersInRole)
                {
                    users.Add(user);
                }
            }

            return users;
        }

        /// <summary>
        /// Retrieves all users that belong to the specified role.
        /// </summary>
        /// <param name="role">The name of the role.</param>
        /// <returns>A task representing the asynchronous operation, containing the collection of users in the specified role.</returns>
        public async Task<IEnumerable<User>> GetUsersInRoleAsync(string role) =>
            await _userManager.GetUsersInRoleAsync(role);

        /// <summary>
        /// Extracts role names from the ClaimsPrincipal using its ClaimsIdentity's RoleClaimType.
        /// </summary>
        /// <param name="userClaims">The ClaimsPrincipal containing the claims.</param>
        /// <returns>An enumerable of role names found in the claims.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the ClaimsPrincipal does not have a ClaimsIdentity.</exception>
        public IEnumerable<string> GetRoles(ClaimsPrincipal userClaims)
        {
            var claimsIdentity = userClaims.Identity as ClaimsIdentity
                ?? throw new InvalidOperationException("No Identity in user claims.");

            var roles = claimsIdentity.FindAll(claimsIdentity.RoleClaimType)
                .Select(c => c.Value);

            return roles;
        }
    }
}
