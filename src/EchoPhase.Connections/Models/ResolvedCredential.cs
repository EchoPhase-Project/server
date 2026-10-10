// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;

namespace EchoPhase.Connections.Models
{
    /// <summary>
    /// What a provider needs to authenticate a single outgoing request.
    /// </summary>
    public sealed record ResolvedCredential(
        Guid ConnectionId,
        string Provider,
        ConnectionAuthType AuthType,
        string AccessToken,
        string? ClientId);
}
