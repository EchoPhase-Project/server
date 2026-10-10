// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using EchoPhase.Types.Claims;

namespace EchoPhase.Security.Authentication.Jwt.Verdicts
{
    /// <summary>
    /// The claims that decide whether a token is still valid. Tokens sharing
    /// them share the verdict.
    /// </summary>
    public static class PrincipalVerdictKey
    {
        public static Guid? UserId(ClaimsPrincipal principal)
            => Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;

        public static string For(ClaimsPrincipal principal)
            => string.Join('|',
                principal.FindFirstValue(JwtRegisteredClaimNames.Sub),
                principal.FindFirstValue(JwtRegisteredClaimNames.Jti),
                principal.FindFirstValue(JwtRegisteredClaimNames.Exp),
                principal.FindFirstValue(EchoPhaseClaimTypes.ClientId),
                principal.FindFirstValue(EchoPhaseClaimTypes.ImpersonationId));

        /// <summary>
        /// Hashed form for storage: <c>jti</c> is the security stamp and must
        /// not sit in Redis in the clear.
        /// </summary>
        public static string Hashed(ClaimsPrincipal principal)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(For(principal))));
    }
}
