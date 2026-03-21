// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Security.Claims;

namespace EchoPhase.Security.Authentication.Jwt.Claims.Providers
{
    public interface IClaimsProvider
    {
        Task EnrichAsync(ClaimsIdentity identity, ClaimsEnrichmentContext context);
    }
}
