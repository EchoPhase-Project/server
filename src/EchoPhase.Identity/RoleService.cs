// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Security.Claims;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Security.BitMasks;
using EchoPhase.Security.BitMasks.Constants;
using EchoPhase.Types.Result;
using Microsoft.AspNetCore.Identity;

namespace EchoPhase.Identity
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
        /// Creates a role if it does not exist yet.
        /// </summary>
        /// <param name="roleName">The name of the role to create.</param>
        /// <returns>Fails with <see cref="RoleErrors.UnknownRole"/> or <see cref="RoleErrors.Invalid"/>.</returns>
        public async Task<IServiceResult> CreateRoleAsync(string roleName)
        {
            if (Unknown([roleName]) is { } unknown)
                return unknown;

            if (await _roleManager.RoleExistsAsync(roleName))
                return ServiceResult.Success();

            var result = await _roleManager.CreateAsync(new UserRole { Name = roleName });
            return result.Succeeded ? ServiceResult.Success() : Refused("create role", roleName, result);
        }

        /// <summary>
        /// Adds the user to the roles, creating a role on first use. Nothing changes
        /// when one of them is not a role defined in code.
        /// </summary>
        /// <param name="user">The user to add to roles.</param>
        /// <param name="roleNames">The role names.</param>
        /// <returns>Fails with <see cref="RoleErrors.UnknownRole"/> or <see cref="RoleErrors.Invalid"/>.</returns>
        public async Task<IServiceResult> AddToRolesAsync(User user, params IEnumerable<string> roleNames)
        {
            var roles = roleNames.ToArray();
            if (Unknown(roles) is { } unknown)
                return unknown;

            var changed = false;
            foreach (var roleName in roles)
            {
                var created = await CreateRoleAsync(roleName);
                if (!created.Successful)
                    return created;

                if (await IsInRoleAsync(user, roleName))
                    continue;

                var result = await _userManager.AddToRoleAsync(user, roleName);
                if (!result.Succeeded)
                    return Refused("add to role", roleName, result);
                changed = true;
            }

            if (changed)
                await InvalidateTokensAsync(user);
            return ServiceResult.Success();
        }

        /// <summary>
        /// Adds the user to one role; see <see cref="AddToRolesAsync"/>.
        /// </summary>
        public Task<IServiceResult> AddToRoleAsync(User user, string roleName)
            => AddToRolesAsync(user, roleName);

        /// <summary>
        /// Removes the user from the roles. Nothing changes when one of them is not
        /// a role defined in code.
        /// </summary>
        /// <param name="user">The user to remove from roles.</param>
        /// <param name="roleNames">One or more collections of role names.</param>
        /// <returns>Fails with <see cref="RoleErrors.UnknownRole"/> or <see cref="RoleErrors.Invalid"/>.</returns>
        public async Task<IServiceResult> RemoveFromRolesAsync(User user, params IEnumerable<string>[] roleNames)
        {
            var roles = roleNames.SelectMany(r => r).ToArray();
            if (Unknown(roles) is { } unknown)
                return unknown;

            var changed = false;
            foreach (var roleName in roles)
            {
                if (!await IsInRoleAsync(user, roleName))
                    continue;

                var result = await _userManager.RemoveFromRoleAsync(user, roleName);
                if (!result.Succeeded)
                    return Refused("remove from role", roleName, result);
                changed = true;
            }

            if (changed)
                await InvalidateTokensAsync(user);
            return ServiceResult.Success();
        }

        /// <summary>
        /// Removes the user from one role; see <see cref="RemoveFromRolesAsync"/>.
        /// </summary>
        public Task<IServiceResult> RemoveFromRoleAsync(User user, string roleName)
            => RemoveFromRolesAsync(user, new[] { roleName });

        /// <summary>
        /// Access tokens carry permissions derived from roles, so every issued
        /// token must die when roles change; refresh then re-derives them.
        /// </summary>
        private Task InvalidateTokensAsync(User user)
            => _userManager.UpdateSecurityStampAsync(user);

        // Roles are bundles defined in code (RolePermissions); an unknown name
        // would grant nothing and only confuse whoever assigned it.
        private static IServiceResult? Unknown(IEnumerable<string> roleNames)
        {
            var unknown = roleNames.Where(r => !RolePermissions.IsKnownRole(r)).ToArray();
            return unknown.Length == 0
                ? null
                : ServiceResult.Failure(e => e.Set(RoleErrors.UnknownRole,
                    $"Unknown role(s): {string.Join(", ", unknown)}. Known roles: {string.Join(", ", Roles.AsEnumerable())}."));
        }

        private static IServiceResult Refused(string action, string roleName, IdentityResult result)
            => ServiceResult.Failure(e => e.Set(RoleErrors.Invalid,
                $"Failed to {action} {roleName}: {string.Join(", ", result.Errors.Select(x => x.Description))}"));

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
