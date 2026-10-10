// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;

namespace EchoPhase.Security.Authentication.Jwt.Claims
{
    public class ClaimsEnrichmentContext
    {
        public required User User { get; init; }

        /// <summary>
        /// Set when issuing a token to an API client; becomes the <c>client_id</c> claim.
        /// </summary>
        public Guid? ClientId { get; init; }

        /// <summary>
        /// Set when issuing an impersonation token: <see cref="User"/> is the
        /// target, this is who actually acts. Becomes the <c>act</c> claim.
        /// </summary>
        public Guid? ActorId { get; init; }

        /// <summary>
        /// The impersonation record the token belongs to; becomes the <c>imp</c> claim.
        /// </summary>
        public Guid? ImpersonationId { get; init; }

        /// <summary>
        /// Permissions to narrow the token to. <c>null</c> (interactive sessions)
        /// grants the full ceiling of the user's roles; otherwise the token gets
        /// the intersection of this with that ceiling.
        /// </summary>
        public IReadOnlyDictionary<string, string[]>? RequestedPermissions { get; init; }
    }
}
