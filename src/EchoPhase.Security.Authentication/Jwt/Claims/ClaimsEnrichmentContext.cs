// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;

namespace EchoPhase.Security.Authentication.Jwt.Claims
{
    public class ClaimsEnrichmentContext
    {
        public required User User { get; init; }

        public IReadOnlyList<string> RequestedScopes { get; init; } = [];
        public IReadOnlyList<string> RequestedIntents { get; init; } = [];

        public IReadOnlyDictionary<string, string[]> RequestedPermissions { get; init; }  // string[] → string
            = new Dictionary<string, string[]>();
    }
}
