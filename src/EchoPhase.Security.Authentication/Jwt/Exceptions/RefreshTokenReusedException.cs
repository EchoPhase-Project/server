// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Security.Authentication.Jwt.Exceptions
{
    public class RefreshTokenReusedException : UnauthorizedAccessException
    {
        public Guid UserId { get; }

        public RefreshTokenReusedException(Guid userId)
            : base("Refresh token reuse detected.")
        {
            UserId = userId;
        }
    }
}
