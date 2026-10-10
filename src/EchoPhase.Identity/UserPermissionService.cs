// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Security.BitMasks;
using EchoPhase.Types.Result;
using Microsoft.AspNetCore.Identity;

namespace EchoPhase.Identity
{
    public class UserPermissionService : IUserPermissionService
    {
        private const int MaxReasonLength = 500;

        private readonly IRoleService _roles;
        private readonly UserPermissionGrantRepository _grants;
        private readonly UserManager<User> _userManager;
        private readonly TimeProvider _time;

        public UserPermissionService(
            IRoleService roles,
            UserPermissionGrantRepository grants,
            UserManager<User> userManager,
            TimeProvider time)
        {
            _roles = roles;
            _grants = grants;
            _userManager = userManager;
            _time = time;
        }

        public async Task<IReadOnlyDictionary<string, string[]>> GetCeilingAsync(User user, CancellationToken ct = default)
        {
            var roles = await _roles.GetRolesAsync(user);
            var grants = await ActiveGrantsAsync(user.Id, ct);
            return RolePermissions.CeilingFor(roles, ToEntries(grants));
        }

        public async Task<UserPermissionsView> DescribeAsync(User user, CancellationToken ct = default)
        {
            var roles = (await _roles.GetRolesAsync(user)).Order(StringComparer.Ordinal).ToList();

            // Expired grants are listed too: they explain a recent loss of access.
            var grants = await _grants.Query()
                .WithUserIds(user.Id)
                .OrderBy(g => g.Resource)
                .AsNoTracking()
                .ToListAsync(ct);

            var now = _time.GetUtcNow().UtcDateTime;
            var effective = RolePermissions.CeilingFor(roles, ToEntries(grants.Where(g => g.IsActive(now))));
            return new UserPermissionsView(roles, grants, effective);
        }

        public async Task<IServiceResult<UserPermissionGrant>> GrantAsync(
            User actor, User target, string resource, string[] permissions,
            string reason, DateTimeOffset? expiresAt, CancellationToken ct = default)
        {
            reason = reason.Trim();
            var actions = permissions.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var requested = new Dictionary<string, string[]>(StringComparer.Ordinal) { [resource] = actions };

            if (actions.Length == 0)
                return Failure<UserPermissionGrant>(UserPermissionErrors.Invalid, "At least one permission is required.");

            if (reason.Length == 0 || reason.Length > MaxReasonLength)
                return Failure<UserPermissionGrant>(UserPermissionErrors.Invalid,
                    $"A reason of 1-{MaxReasonLength} characters is required.");

            if (expiresAt is { } at && at <= _time.GetUtcNow())
                return Failure<UserPermissionGrant>(UserPermissionErrors.Invalid, "ExpiresAt must be in the future.");

            var unknown = RolePermissions.Unknown(requested);
            if (unknown.Count > 0)
                return Failure<UserPermissionGrant>(UserPermissionErrors.Invalid,
                    $"Unknown permissions: {string.Join(", ", unknown)}.");

            var roleOnly = actions.Where(a => !RolePermissions.IsGrantable(resource, a)).ToArray();
            if (roleOnly.Length > 0)
                return Failure<UserPermissionGrant>(UserPermissionErrors.Forbidden,
                    $"Only a role can give: {string.Join(", ", roleOnly.Select(a => $"{resource}:{a}"))}.");

            // Granting cannot be a way to exceed one's own access.
            var exceeding = RolePermissions.Exceeding(await GetCeilingAsync(actor, ct), requested);
            if (exceeding.Count > 0)
                return Failure<UserPermissionGrant>(UserPermissionErrors.Forbidden,
                    $"You do not hold: {string.Join(", ", exceeding)}.");

            var grant = await _grants.Query()
                .WithUserIds(target.Id)
                .WithResources(resource)
                .FirstOrDefaultAsync(ct);

            if (grant is null)
            {
                grant = new UserPermissionGrant { UserId = target.Id, Resource = resource };
                _grants.Add(grant);
            }

            grant.Permissions = actions;
            grant.Reason = reason;
            grant.ExpiresAt = expiresAt?.UtcDateTime;
            grant.ExpiryApplied = false;
            grant.GrantedById = actor.Id;

            await RevokeTokensAsync(target);
            return ServiceResult<UserPermissionGrant>.Success(grant);
        }

        public async Task<IServiceResult> RevokeAsync(User target, string resource, CancellationToken ct = default)
        {
            var grant = await _grants.Query()
                .WithUserIds(target.Id)
                .WithResources(resource)
                .FirstOrDefaultAsync(ct);

            if (grant is null)
                return ServiceResult.Failure(e => e.Set(UserPermissionErrors.NotFound, "Grant not found."));

            _grants.Remove(grant);
            await RevokeTokensAsync(target);
            return ServiceResult.Success();
        }

        /// <summary>
        /// Saves the pending grant change together with a new security stamp:
        /// the Identity store shares the context, so both land in one SaveChanges.
        /// Old tokens then fail the stamp check and sessions refresh into the new
        /// permissions; the verdict cache is dropped by its interceptor.
        /// </summary>
        private async Task RevokeTokensAsync(User target)
        {
            var result = await _userManager.UpdateSecurityStampAsync(target);
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    $"Could not update the security stamp: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }

        private Task<List<UserPermissionGrant>> ActiveGrantsAsync(Guid userId, CancellationToken ct)
            => _grants.Query()
                .WithUserIds(userId)
                .Active(_time.GetUtcNow().UtcDateTime)
                .AsNoTracking()
                .ToListAsync(ct);

        private static IEnumerable<KeyValuePair<string, string[]>> ToEntries(IEnumerable<UserPermissionGrant> grants)
            => grants.Select(g => new KeyValuePair<string, string[]>(g.Resource, g.Permissions));

        private static IServiceResult<T> Failure<T>(string code, string message)
            => ServiceResult<T>.Failure(e => e.Set(code, message));
    }
}
