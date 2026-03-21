// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

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
    }
}
