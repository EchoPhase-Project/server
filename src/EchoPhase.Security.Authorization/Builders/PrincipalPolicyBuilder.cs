// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Security.Authorization.Requirements;
using Microsoft.AspNetCore.Authorization;

namespace EchoPhase.Security.Authorization.Builders
{
    [PolicyPrefix("principal:")]
    public class PrincipalPolicyBuilder : IPolicyBuilder
    {
        public AuthorizationPolicy? Build(string body)
        {
            PrincipalKind? kind = body switch
            {
                "session" => PrincipalKind.Session,
                "api-client" => PrincipalKind.ApiClient,
                "impersonation" => PrincipalKind.Impersonation,
                _ => null
            };

            if (kind is null) return null;

            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PrincipalRequirement(kind.Value))
                .Build();
        }
    }
}
