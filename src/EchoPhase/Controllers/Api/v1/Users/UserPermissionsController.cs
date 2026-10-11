// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Identity;
using EchoPhase.Security.Antiforgery.Attributes;
using EchoPhase.Security.Authentication.Extensions;
using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Security.BitMasks.Constants;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Controllers.Api.v1.Users
{
    /// <summary>
    /// A user's permissions: what their roles give, explicit grants on top,
    /// and the effective result that ends up in their tokens.
    /// </summary>
    [Route("api/v1/users")]
    public class UserPermissionsController : ApiControllerBase
    {
        private readonly IUserPermissionService _permissions;
        private readonly UserRepository _users;
        private readonly TimeProvider _time;

        public UserPermissionsController(IUserPermissionService permissions, UserRepository users, TimeProvider time)
        {
            _permissions = permissions;
            _users = users;
            _time = time;
        }

        /// <summary>The caller's roles, grants and effective permissions.</summary>
        [HttpGet("me/permissions")]
        public async Task<IActionResult> GetMine(CancellationToken ct = default)
        {
            var user = await _users.Query().WithIds(User.GetUserId()).FirstOrDefaultAsync(ct);
            return user is null ? Unauthorized() : Ok(View(await _permissions.DescribeAsync(user, ct)));
        }

        [HttpGet("{username:username}/permissions")]
        [RequirePermission(Resources.Roles, Permissions.Read)]
        public async Task<IActionResult> Get(string username, CancellationToken ct = default)
        {
            var user = await FindAsync(username, ct);
            return user is null ? UserNotFound(username) : Ok(View(await _permissions.DescribeAsync(user, ct)));
        }

        /// <summary>
        /// Grants actions on one resource, replacing an earlier grant for it.
        /// Only what the caller holds, never a role-only action. The user's
        /// tokens are revoked so sessions refresh into the new permissions.
        /// </summary>
        [HttpPut("{username:username}/permissions/{resource}")]
        [ValidateAntiForgery]
        [RequirePrincipal(PrincipalKind.Session)]
        [RequirePermission(Resources.Users, Permissions.Grant)]
        public async Task<IActionResult> Grant(
            string username, string resource, [FromBody] GrantPermissionsRequest dto, CancellationToken ct = default)
        {
            var actor = await _users.Query().WithIds(User.GetUserId()).FirstOrDefaultAsync(ct);
            if (actor is null)
                return Unauthorized();

            var target = await FindAsync(username, ct);
            if (target is null)
                return UserNotFound(username);

            var result = await _permissions.GrantAsync(actor, target, resource, dto.Permissions, dto.Reason, dto.ExpiresAt, ct);
            return result.Successful ? Ok(View(result.Value!, _time.GetUtcNow().UtcDateTime)) : Failure(result.Error);
        }

        [HttpDelete("{username:username}/permissions/{resource}")]
        [ValidateAntiForgery]
        [RequirePrincipal(PrincipalKind.Session)]
        [RequirePermission(Resources.Users, Permissions.Grant)]
        public async Task<IActionResult> Revoke(string username, string resource, CancellationToken ct = default)
        {
            var target = await FindAsync(username, ct);
            if (target is null)
                return UserNotFound(username);

            var result = await _permissions.RevokeAsync(target, resource, ct);
            return result.Successful ? NoContent() : Failure(result.Error);
        }

        private Task<User?> FindAsync(string username, CancellationToken ct)
            => _users.Query().WithUserNames(username).FirstOrDefaultAsync(ct);

        private object View(UserPermissionsView view)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            return new
            {
                roles = view.Roles,
                grants = view.Grants.Select(g => View(g, now)),
                effective = view.Effective
            };
        }

        private static object View(UserPermissionGrant grant, DateTime now) => new
        {
            resource = grant.Resource,
            permissions = grant.Permissions,
            reason = grant.Reason,
            expiresAt = grant.ExpiresAt,
            active = grant.IsActive(now),
            grantedById = grant.GrantedById,
            createdAt = grant.CreatedAt,
            updatedAt = grant.UpdatedAt
        };

        private ObjectResult UserNotFound(string username)
            => Error(UserPermissionErrors.NotFound, $"User '{username}' not found.", StatusCodes.Status404NotFound);
    }
}
