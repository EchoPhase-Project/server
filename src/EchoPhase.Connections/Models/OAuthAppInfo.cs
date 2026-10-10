// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;

namespace EchoPhase.Connections.Models
{
    /// <summary>
    /// Public view of a user-owned OAuth application; the secret is never returned.
    /// </summary>
    public sealed record OAuthAppInfo(
        Guid Id,
        string Provider,
        string Name,
        string ClientId,
        bool HasSecret,
        DateTime CreatedAt)
    {
        public static OAuthAppInfo From(OAuthApp a) => new(
            a.Id, a.Provider, a.Name, a.ClientId, a.ClientSecret is { Length: > 0 }, a.CreatedAt);
    }
}
