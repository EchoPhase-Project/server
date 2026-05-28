// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Identity;
using EchoPhase.Security.Antiforgery.Attributes;
using EchoPhase.Security.Authentication.Attributes;
using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Security.BitMasks.Constants;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Controllers.Api.v1
{
    [ApiController]
    [Route("api/v1/roles")]
    [RequireUser]
    public class RolesController : ControllerBase
    {
        private readonly IRoleService _roleService;
        private readonly IUserService _userService;
        private readonly UserRepository _userRepository;

        public RolesController(
            IRoleService roleService,
            IUserService userService,
            UserRepository userRepository)
        {
            _roleService = roleService;
            _userService = userService;
            _userRepository = userRepository;
        }

        private User CurrentUser =>
            (User)HttpContext.Items["User"]!;

        /// <summary>
        /// Returns all roles assigned to the currently authenticated user.
        /// </summary>
        [HttpGet("me")]
        public async Task<IActionResult> GetMyRoles()
        {
            var roles = await _roleService.GetRolesAsync(CurrentUser);
            return Ok(roles);
        }

        /// <summary>
        /// Returns all roles assigned to the specified user. Requires admin role.
        /// </summary>
        [HttpGet("users/{username}")]
        [RequireRole(Roles.Admin)]
        public async Task<IActionResult> GetUserRoles([FromRoute] string username)
        {
            var targetUser = await _userService.GetAsync(User);
            if (targetUser is null)
                return NotFound($"User '{username}' not found.");

            var roles = await _roleService.GetRolesAsync(targetUser);
            return Ok(new { Username = username, Roles = roles });
        }

        /// <summary>
        /// Returns all users belonging to the specified role. Requires admin role.
        /// </summary>
        [HttpGet("{roleName}/users")]
        [RequireRole(Roles.Admin)]
        public async Task<IActionResult> GetUsersInRole([FromRoute] string roleName)
        {
            var users = await _roleService.GetUsersInRoleAsync(roleName);
            return Ok(users.Select(u => new { u.Id, u.UserName, u.Name }));
        }

        /// <summary>
        /// Assigns one or more roles to a user. Requires admin role + antiforgery token.
        /// </summary>
        [HttpPost("users/{username}")]
        [RequireRole(Roles.Admin)]
        [ValidateAntiForgery]
        public async Task<IActionResult> AssignRoles(
            [FromRoute] string username,
            [FromBody] RoleAssignmentDto dto)
        {
            if (dto.Roles is null || dto.Roles.Length == 0)
                return BadRequest("At least one role is required.");

            var targetUser = _userRepository.Query().WithUserNames(username).FirstOrDefault();
            if (targetUser is null)
                return NotFound($"User '{username}' not found.");

            await _roleService.AddToRolesAsync(targetUser, dto.Roles);
            return NoContent();
        }

        /// <summary>
        /// Removes one or more roles from a user. Requires admin role + antiforgery token.
        /// </summary>
        [HttpDelete("users/{username}")]
        [RequireRole(Roles.Admin)]
        [ValidateAntiForgery]
        public async Task<IActionResult> RevokeRoles(
            [FromRoute] string username,
            [FromBody] RoleAssignmentDto dto)
        {
            if (dto.Roles is null || dto.Roles.Length == 0)
                return BadRequest("At least one role is required.");

            var targetUser = _userRepository.Query().WithUserNames(username).FirstOrDefault();
            if (targetUser is null)
                return NotFound($"User '{username}' not found.");

            await _roleService.RemoveFromRolesAsync(targetUser, dto.Roles);
            return NoContent();
        }

        /// <summary>
        /// Creates a new role. Requires admin role + antiforgery token.
        /// </summary>
        [HttpPost]
        [RequireRole(Roles.Admin)]
        [ValidateAntiForgery]
        public async Task<IActionResult> CreateRole([FromBody] RoleNameDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest("Role name is required.");

            await _roleService.CreateRoleAsync(dto.Name);
            return NoContent();
        }
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────

    public record RoleAssignmentDto(string[] Roles);

    public record RoleNameDto(string Name);
}
