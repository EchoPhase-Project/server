// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions.Repository;
using EchoPhase.DAL.Postgres.Models;

namespace EchoPhase.Security.Authentication.Jwt.Providers
{
    public interface ISessionProvider
    {
        Task<TokenInitial> CreateAsync(User user, string deviceId, CancellationToken ct = default);

        /// <summary>
        /// Rotates the session's refresh token and issues a new access token.
        /// When the token was rotated moments ago by a parallel request, only an
        /// access token is returned (<see cref="TokenPair.RefreshToken"/> is <c>null</c>).
        /// </summary>
        /// <exception cref="Exceptions.RefreshTokenReusedException">
        /// An old token was replayed outside the grace period; the session is revoked.
        /// </exception>
        /// <exception cref="UnauthorizedAccessException">The token is unknown.</exception>
        Task<TokenPair> RefreshAsync(string refreshToken, CancellationToken ct = default);

        Task RevokeAsync(Guid userId, Guid sessionId, CancellationToken ct = default);

        /// <summary>
        /// Revokes the session the given refresh token belongs to, if it is the user's.
        /// </summary>
        Task RevokeByTokenAsync(Guid userId, string refreshToken, CancellationToken ct = default);

        Task RevokeAllAsync(Guid userId, CancellationToken ct = default);

        Task<CursorPage<Session>> GetSessionsAsync(
            Guid userId, CursorOptions? cursor = null, CancellationToken ct = default);
    }
}
