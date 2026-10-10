// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Security.Claims;
using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Types.Claims;

namespace EchoPhase.Security.Authorization
{
    public static class PrincipalKinds
    {
        /// <summary>
        /// Who the token was issued to. An impersonation token also carries the
        /// target's identity, so <c>act</c> is checked first.
        /// </summary>
        public static PrincipalKind Of(ClaimsPrincipal principal)
            => principal.HasClaim(c => c.Type == EchoPhaseClaimTypes.Act) ? PrincipalKind.Impersonation
             : principal.HasClaim(c => c.Type == EchoPhaseClaimTypes.ClientId) ? PrincipalKind.ApiClient
             : PrincipalKind.Session;
    }
}
