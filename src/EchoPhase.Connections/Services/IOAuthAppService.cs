// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Connections.Models;
using EchoPhase.Types.Result;

namespace EchoPhase.Connections.Services
{
    public interface IOAuthAppService
    {
        Task<IReadOnlyList<OAuthAppInfo>> ListAsync(Guid userId, CancellationToken ct = default);

        /// <summary>
        /// Creates the app, or updates name/secret when the user already
        /// registered the same client id for this provider.
        /// </summary>
        Task<IServiceResult<OAuthAppInfo>> SaveAsync(
            Guid userId, string provider, string name, string clientId, string? clientSecret,
            CancellationToken ct = default);

        Task<IServiceResult> DeleteAsync(Guid userId, Guid appId, CancellationToken ct = default);

        /// <summary>
        /// Resolves the client credentials for a connection's app; <paramref name="appId"/>
        /// <c>null</c> selects the platform app.
        /// </summary>
        Task<IServiceResult<OAuthClient>> ResolveClientAsync(
            string provider, Guid? appId, CancellationToken ct = default);

        /// <summary>
        /// Picks the app a manually supplied token belongs to, registering a
        /// user-owned app when needed.
        /// </summary>
        Task<IServiceResult<Guid?>> MatchManualAsync(
            Guid userId, string provider, string clientId, string? clientSecret,
            CancellationToken ct = default);

        Task<bool> IsOwnedAsync(Guid userId, string provider, Guid appId, CancellationToken ct = default);
    }
}
