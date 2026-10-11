// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Types.Result;

namespace EchoPhase.Security.Authentication
{
    /// <summary>Error codes of signing in and refreshing a session.</summary>
    public static class AuthErrors
    {
        /// <summary>Unknown user or wrong password (not told apart).</summary>
        public const string InvalidCredentials = ErrorCodes.InvalidCredentials;
        public const string LockedOut = "LockedOut";
        /// <summary>The refresh token is unknown or its session is gone.</summary>
        public const string InvalidRefreshToken = "InvalidRefreshToken";
        /// <summary>An old refresh token was replayed: the session was revoked.</summary>
        public const string RefreshTokenReused = "RefreshTokenReused";
    }

    /// <summary>
    /// <see cref="AuthErrors.RefreshTokenReused"/>, with the user whose session it
    /// was, so that their other tokens can be invalidated too.
    /// </summary>
    public sealed class RefreshTokenReusedError : ServiceError
    {
        public Guid UserId { get; }

        public RefreshTokenReusedError(Guid userId)
            : base(AuthErrors.RefreshTokenReused, "Refresh token reuse detected.")
            => UserId = userId;
    }
}
