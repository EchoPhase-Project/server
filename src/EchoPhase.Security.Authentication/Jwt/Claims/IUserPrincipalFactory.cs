// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Security.Claims;
using EchoPhase.DAL.Postgres.Models;

namespace EchoPhase.Security.Authentication.Jwt.Claims
{
    public interface IUserPrincipalFactory
    {
        Task<ClaimsPrincipal> CreateAsync(User user, ClaimsEnrichmentContext? context = null);
    }
}
