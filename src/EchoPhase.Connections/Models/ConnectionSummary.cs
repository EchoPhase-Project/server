// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;

namespace EchoPhase.Connections.Models
{
    /// <summary>
    /// Public view of a connection; never contains token material.
    /// </summary>
    public sealed record ConnectionSummary(
        Guid Id,
        string Provider,
        string Name,
        string AuthType,
        string Status,
        Guid? OAuthAppId,
        string? ExternalAccountId,
        string? ExternalLogin,
        IReadOnlyList<string> Scopes,
        DateTime? ExpiresAt,
        DateTime? LastValidatedAt,
        string? LastError,
        DateTime CreatedAt,
        DateTime UpdatedAt)
    {
        public static ConnectionSummary From(Connection c) => new(
            c.Id, c.Provider, c.Name,
            c.AuthType.ToString(), c.Status.ToString(),
            c.OAuthAppId, c.ExternalAccountId, c.ExternalLogin, c.Scopes,
            c.ExpiresAt, c.LastValidatedAt, c.LastError,
            c.CreatedAt, c.UpdatedAt);
    }
}
