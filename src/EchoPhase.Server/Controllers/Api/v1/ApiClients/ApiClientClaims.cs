// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Server.Controllers.Api.v1.ApiClients
{
    public sealed record ApiClientClaims(
        IReadOnlyDictionary<string, string[]> Permissions = default!
    );
}
