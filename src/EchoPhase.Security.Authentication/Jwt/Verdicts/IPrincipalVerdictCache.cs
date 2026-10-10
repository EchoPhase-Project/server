// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Security.Claims;

namespace EchoPhase.Security.Authentication.Jwt.Verdicts
{
    /// <summary>
    /// Short-lived cache of "this token is still valid" verdicts, so the
    /// database checks behind a JWT run once per TTL instead of per request.
    /// Only positive verdicts are cached: a revoked token never becomes valid again.
    /// </summary>
    public interface IPrincipalVerdictCache
    {
        Task<bool> IsKnownValidAsync(ClaimsPrincipal principal);

        Task RememberValidAsync(ClaimsPrincipal principal);

        /// <summary>Drops every cached verdict of the users, on all instances.</summary>
        Task InvalidateAsync(IEnumerable<Guid> userIds);
    }
}
