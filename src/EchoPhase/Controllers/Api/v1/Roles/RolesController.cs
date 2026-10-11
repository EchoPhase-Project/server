// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Identity;
using EchoPhase.Security.Antiforgery.Attributes;
using EchoPhase.Security.Authentication.Extensions;
using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Security.BitMasks;
using EchoPhase.Security.BitMasks.Constants;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Controllers.Api.v1.Roles
{
    [Route("api/v1/roles")]
    public class RolesController : ApiControllerBase
    {
        private readonly IRoleService _roleService;
        private readonly UserRepository _userRepository;

        public RolesController(
            IRoleService roleService,
            UserRepository userRepository)
        {
            _roleService = roleService;
            _userRepository = userRepository;
        }

        // Identity's role API takes the entity, so only the actions that need
        // it load the user; authentication has already checked it exists.
        private Task<User?> FindUserAsync(Guid id, CancellationToken ct)
            => _userRepository.Query().WithIds(id).FirstOrDefaultAsync(ct);

        private Task<User?> FindUserAsync(string username, CancellationToken ct)
            => _userRepository.Query().WithUserNames(username).FirstOrDefaultAsync(ct);

        /// <summary>
        /// Returns all roles assigned to the currently authenticated user.
        /// </summary>
        [HttpGet("me")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<IReadOnlyList<string>>> GetMyRoles(CancellationToken ct = default)
        {
            var user = await FindUserAsync(User.GetUserId(), ct);
            if (user is null)
                return Unauthorized();

            return (await _roleService.GetRolesAsync(user)).ToList();
        }

        /// <summary>
        /// The fixed set of roles and the permissions each adds on top of the
        /// ones every user has.
        /// </summary>
        [HttpGet]
        public IActionResult GetRoles()
            => Ok(new { everyone = RolePermissions.Everyone, roles = RolePermissions.ByRole });

        /// <summary>
        /// Returns all roles assigned to the specified user. Requires roles:read.
        /// </summary>
        [HttpGet("users/{username}")]
        [RequirePermission(Resources.Roles, Permissions.Read)]
        public async Task<IActionResult> GetUserRoles([FromRoute] string username, CancellationToken ct = default)
        {
            var targetUser = await FindUserAsync(username, ct);
            if (targetUser is null)
                return NotFound($"User '{username}' not found.");

            var roles = await _roleService.GetRolesAsync(targetUser);
            return Ok(new { Username = username, Roles = roles });
        }

        /// <summary>
        /// Returns all users belonging to the specified role. Requires roles:read.
        /// </summary>
        [HttpGet("{roleName}/users")]
        [RequirePermission(Resources.Roles, Permissions.Read)]
        public async Task<IActionResult> GetUsersInRole([FromRoute] string roleName)
        {
            var users = await _roleService.GetUsersInRoleAsync(roleName);
            return Ok(users.Select(u => new { u.Id, u.UserName, u.Name }));
        }

        /// <summary>Gives the user one role; already having it is not an error.</summary>
        [HttpPut("{roleName}/users/{username}")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Roles, Permissions.Assign)]
        public async Task<IActionResult> AssignRole(
            [FromRoute] string roleName,
            [FromRoute] string username,
            CancellationToken ct = default)
        {
            var targetUser = await FindUserAsync(username, ct);
            if (targetUser is null)
                return NotFound($"User '{username}' not found.");

            var result = await _roleService.AddToRolesAsync(targetUser, roleName);
            return result.Successful ? NoContent() : Failure(result.Error);
        }

        /// <summary>Takes one role away from the user.</summary>
        [HttpDelete("{roleName}/users/{username}")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Roles, Permissions.Assign)]
        public async Task<IActionResult> RevokeRole(
            [FromRoute] string roleName,
            [FromRoute] string username,
            CancellationToken ct = default)
        {
            var targetUser = await FindUserAsync(username, ct);
            if (targetUser is null)
                return NotFound($"User '{username}' not found.");

            var result = await _roleService.RemoveFromRolesAsync(targetUser, new[] { roleName });
            return result.Successful ? NoContent() : Failure(result.Error);
        }
    }

}
