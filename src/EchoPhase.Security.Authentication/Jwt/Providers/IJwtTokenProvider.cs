// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Security.Claims;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Security.Authentication.Jwt.Claims;

namespace EchoPhase.Security.Authentication.Jwt.Providers
{
    public interface IJwtTokenProvider
    {
        Task<string> GenerateTokenAsync(
            User user,
            TimeSpan? lifetime = null,
            ClaimsEnrichmentContext? context = null);
        Task<ClaimsPrincipal?> ValidateTokenAsync(string token, CancellationToken ct = default);

        /// <summary>
        /// Re-checks a principal from an already validated token, for long-lived
        /// connections: not expired, same security stamp, and its API client or
        /// impersonation still active.
        /// </summary>
        Task<bool> IsPrincipalValidAsync(ClaimsPrincipal principal, CancellationToken ct = default);
    }
}
