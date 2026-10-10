// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using EchoPhase.Types.Claims;

namespace EchoPhase.Security.Authentication.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static Guid GetUserId(this ClaimsPrincipal principal) =>
            Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? throw new InvalidOperationException("UserId claim missing."));

        public static string GetUserName(this ClaimsPrincipal principal) =>
            principal.FindFirstValue(JwtRegisteredClaimNames.Name)
                ?? throw new InvalidOperationException("UserName claim missing.");

        /// <summary>
        /// The real user behind an impersonation token (<c>act.sub</c>), or null
        /// for any other token.
        /// </summary>
        public static Guid? GetActorId(this ClaimsPrincipal principal)
        {
            var act = principal.FindFirstValue(EchoPhaseClaimTypes.Act);
            if (act is null)
                return null;

            try
            {
                using var doc = JsonDocument.Parse(act);
                return doc.RootElement.TryGetProperty(JwtRegisteredClaimNames.Sub, out var sub)
                       && Guid.TryParse(sub.GetString(), out var id)
                    ? id
                    : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
