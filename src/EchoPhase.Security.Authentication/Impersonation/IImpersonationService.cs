// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Types.Result;

namespace EchoPhase.Security.Authentication.Impersonation
{
    public interface IImpersonationService
    {
        /// <summary>
        /// Starts acting as <paramref name="targetUserName"/>. The actor confirms
        /// with their password and must outrank the target.
        /// </summary>
        Task<IServiceResult<ImpersonationStarted>> StartAsync(
            Guid actorId, string targetUserName, string reason, string password, CancellationToken ct = default);

        /// <summary>Ends an impersonation the actor started.</summary>
        Task<IServiceResult> EndAsync(Guid actorId, Guid impersonationId, CancellationToken ct = default);

        /// <summary>Ends the impersonation a token belongs to, from that token.</summary>
        Task<IServiceResult> EndCurrentAsync(Guid targetId, Guid impersonationId, CancellationToken ct = default);

        /// <summary>Recent impersonations the user took part in, as actor or target.</summary>
        Task<IReadOnlyList<DAL.Postgres.Models.Impersonation>> ListAsync(Guid userId, CancellationToken ct = default);
    }
}
