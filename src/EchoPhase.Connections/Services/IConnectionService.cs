// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Clients.Abstractions;
using EchoPhase.Connections.Models;
using EchoPhase.Types.Result;

namespace EchoPhase.Connections.Services
{
    public interface IConnectionService
    {
        Task<IReadOnlyList<ConnectionSummary>> ListAsync(
            Guid userId, string? provider = null, CancellationToken ct = default);

        /// <summary>
        /// Stores a token pasted by the user (bot token, or OAuth tokens from
        /// the user's own app), replacing a connection with the same name.
        /// </summary>
        Task<IServiceResult<ConnectionSummary>> SaveManualAsync(
            Guid userId, string provider, ManualConnectionRequest request, CancellationToken ct = default);

        /// <summary>
        /// Stores tokens obtained through the OAuth flow, replacing a
        /// connection with the same name (reconnect keeps its id).
        /// </summary>
        Task<IServiceResult<ConnectionSummary>> SaveOAuthAsync(
            Guid userId, string provider, string name, Guid? appId,
            OAuthTokenSet tokens, TokenIdentity? identity, CancellationToken ct = default);

        Task<IServiceResult> DeleteAsync(Guid userId, Guid connectionId, CancellationToken ct = default);

        /// <summary>
        /// Returns a usable credential, refreshing it first when it is about to expire.
        /// </summary>
        Task<IServiceResult<ResolvedCredential>> ResolveAsync(
            ConnectionRef reference, string provider, CancellationToken ct = default);

        /// <summary>
        /// Called after the provider rejected <paramref name="rejectedAccessToken"/>:
        /// refreshes the token, or marks the connection for re-authentication
        /// when it cannot be refreshed.
        /// </summary>
        Task<IServiceResult<ResolvedCredential>> RecoverAsync(
            ConnectionRef reference, string provider, string rejectedAccessToken, CancellationToken ct = default);

        /// <summary>
        /// Validates every active OAuth connection, refreshing expiring or
        /// rejected tokens. Used by the background worker.
        /// </summary>
        Task ValidateAllAsync(CancellationToken ct = default);
    }
}
