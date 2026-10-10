// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Types.Result;

namespace EchoPhase.Identity
{
    public sealed record UserPermissionsView(
        IReadOnlyList<string> Roles,
        IReadOnlyList<UserPermissionGrant> Grants,
        IReadOnlyDictionary<string, string[]> Effective);

    /// <summary>
    /// A user's permissions: the bundles of their roles plus explicit,
    /// optionally expiring grants. The single place a ceiling is computed.
    /// </summary>
    public interface IUserPermissionService
    {
        /// <summary>Everything the user may do now: roles plus active grants.</summary>
        Task<IReadOnlyDictionary<string, string[]>> GetCeilingAsync(User user, CancellationToken ct = default);

        Task<UserPermissionsView> DescribeAsync(User user, CancellationToken ct = default);

        /// <summary>
        /// Grants actions on one resource, replacing an earlier grant for it.
        /// The actor can only grant what they hold themselves, never a
        /// role-only action. Revokes the target's tokens so they pick it up.
        /// </summary>
        Task<IServiceResult<UserPermissionGrant>> GrantAsync(
            User actor, User target, string resource, string[] permissions,
            string reason, DateTimeOffset? expiresAt, CancellationToken ct = default);

        /// <summary>Removes the grant on a resource and revokes the target's tokens.</summary>
        Task<IServiceResult> RevokeAsync(User target, string resource, CancellationToken ct = default);
    }
}
