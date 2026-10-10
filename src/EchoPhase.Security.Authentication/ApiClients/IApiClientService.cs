// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Types.Result;

namespace EchoPhase.Security.Authentication.ApiClients
{
    public interface IApiClientService
    {
        Task<IReadOnlyList<ApiClient>> ListAsync(Guid userId, CancellationToken ct = default);

        Task<IServiceResult<ApiClientCreated>> CreateAsync(
            Guid userId, string name, ApiClientGrant grant, DateTimeOffset? expiresAt, CancellationToken ct = default);

        /// <summary>
        /// Issues a new secret. The previous one keeps working until
        /// <see cref="RevokePreviousSecretAsync"/> is called.
        /// </summary>
        Task<IServiceResult<string>> RotateSecretAsync(Guid userId, Guid clientId, CancellationToken ct = default);

        Task<IServiceResult> RevokePreviousSecretAsync(Guid userId, Guid clientId, CancellationToken ct = default);

        Task<IServiceResult> DeleteAsync(Guid userId, Guid clientId, CancellationToken ct = default);

        /// <summary>
        /// Client-credentials grant: exchanges id + secret for a short-lived access token.
        /// </summary>
        Task<IServiceResult<ClientAccessToken>> IssueTokenAsync(Guid clientId, string clientSecret, CancellationToken ct = default);
    }
}
