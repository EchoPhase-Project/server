// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Connections.Models;
using EchoPhase.Types.Result;

namespace EchoPhase.Connections.Services
{
    public interface IConnectionOAuthService
    {
        /// <summary>
        /// Starts the authorisation-code flow for a new or existing connection.
        /// <paramref name="appId"/> selects a user-owned app; <c>null</c> uses the platform app.
        /// </summary>
        Task<IServiceResult<OAuthAuthorization>> BeginAsync(
            Guid userId, string provider, string name, Guid? appId, CancellationToken ct = default);

        /// <summary>
        /// Finishes the flow: checks <paramref name="state"/> against the browser's
        /// <paramref name="nonce"/>, exchanges the code and stores the connection.
        /// </summary>
        Task<IServiceResult<ConnectionSummary>> CompleteAsync(
            string provider, string code, string state, string? nonce, CancellationToken ct = default);
    }
}
