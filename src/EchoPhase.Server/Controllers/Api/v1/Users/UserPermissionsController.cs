// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Identity;
using EchoPhase.Security.Antiforgery.Attributes;
using EchoPhase.Security.Authentication.Extensions;
using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Security.BitMasks.Constants;
using EchoPhase.Server.Controllers.Api.v1.Common;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Server.Controllers.Api.v1.Users
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
        [ProducesResponseType<UserPermissionsResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMine(CancellationToken ct = default)
        {
            var user = await _users.Query().WithIds(User.GetUserId()).FirstOrDefaultAsync(ct);
            return user is null ? Unauthorized() : Ok(UserPermissionsResponse.From(await _permissions.DescribeAsync(user, ct), _time.GetUtcNow().UtcDateTime));
        }

        [HttpGet("{username:username}/permissions")]
        [RequirePermission(Resources.Roles, Permissions.Read)]
        [ProducesResponseType<UserPermissionsResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get(string username, CancellationToken ct = default)
        {
            var user = await FindAsync(username, ct);
            return user is null ? UserNotFound(username) : Ok(UserPermissionsResponse.From(await _permissions.DescribeAsync(user, ct), _time.GetUtcNow().UtcDateTime));
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
        [ProducesResponseType<UserPermissionGrantResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
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
            return result.Successful ? Ok(UserPermissionGrantResponse.From(result.Value, _time.GetUtcNow().UtcDateTime)) : Failure(result.Error);
        }

        [HttpDelete("{username:username}/permissions/{resource}")]
        [ValidateAntiForgery]
        [RequirePrincipal(PrincipalKind.Session)]
        [RequirePermission(Resources.Users, Permissions.Grant)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
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

        private ObjectResult UserNotFound(string username)
            => Error(UserPermissionErrors.NotFound, $"User '{username}' not found.", StatusCodes.Status404NotFound);
    }

    /// <summary>A user's permissions: from their roles, granted one by one, and in effect.</summary>
    /// <param name="Roles">The user's roles.</param>
    /// <param name="Grants">The permissions granted to this user directly.</param>
    /// <param name="Effective">All the user may do now, by resource.</param>
    public sealed record UserPermissionsResponse(
        IReadOnlyList<string> Roles, IReadOnlyList<UserPermissionGrantResponse> Grants,
        IReadOnlyDictionary<string, string[]> Effective)
    {
        public static UserPermissionsResponse From(UserPermissionsView view, DateTime now) => new(
            view.Roles, view.Grants.Select(g => UserPermissionGrantResponse.From(g, now)).ToList(), view.Effective);
    }

    /// <summary>Permissions granted to one user on one resource.</summary>
    /// <param name="Resource">The resource.</param>
    /// <param name="Permissions">The actions granted on it.</param>
    /// <param name="Reason">Why, as the granter wrote it.</param>
    /// <param name="ExpiresAt">When the grant ends; none: never.</param>
    /// <param name="Active">Whether it is in force now.</param>
    /// <param name="GrantedById">Who granted it.</param>
    /// <param name="CreatedAt">When it was granted.</param>
    /// <param name="UpdatedAt">The last change.</param>
    public sealed record UserPermissionGrantResponse(
        string Resource, IReadOnlyList<string> Permissions, string Reason, DateTime? ExpiresAt, bool Active,
        Guid? GrantedById, DateTime CreatedAt, DateTime UpdatedAt)
    {
        public static UserPermissionGrantResponse From(UserPermissionGrant grant, DateTime now) => new(
            grant.Resource, grant.Permissions, grant.Reason, grant.ExpiresAt, grant.IsActive(now),
            grant.GrantedById, grant.CreatedAt, grant.UpdatedAt);
    }
}
