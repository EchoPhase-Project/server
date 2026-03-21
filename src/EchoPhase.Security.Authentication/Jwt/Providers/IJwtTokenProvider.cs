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
        Task<ClaimsPrincipal?> ValidateTokenAsync(string token);
    }
}
