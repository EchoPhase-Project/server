// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.AspNetCore.Authorization;

namespace EchoPhase.Security.Authorization.Attributes
{
    /// <summary>
    /// Restricts an endpoint to tokens of one kind, e.g. credential management
    /// to interactive sessions so a leaked client secret cannot mint new secrets.
    /// </summary>
    public class RequirePrincipalAttribute : AuthorizeAttribute
    {
        public RequirePrincipalAttribute(PrincipalKind kind)
            : base(BuildPolicyName(kind)) { }

        internal static string BuildPolicyName(PrincipalKind kind) => kind switch
        {
            PrincipalKind.Session => "principal:session",
            PrincipalKind.ApiClient => "principal:api-client",
            PrincipalKind.Impersonation => "principal:impersonation",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }
}
