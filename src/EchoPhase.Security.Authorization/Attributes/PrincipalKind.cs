// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Security.Authorization.Attributes
{
    /// <summary>
    /// Who a token was issued to.
    /// </summary>
    public enum PrincipalKind
    {
        /// <summary>An interactive login (browser session).</summary>
        Session,

        /// <summary>A bot, script or server using the client-credentials grant.</summary>
        ApiClient,

        /// <summary>A privileged user acting as someone else (token has <c>act</c>).</summary>
        Impersonation
    }
}
