// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Security.Authorization.Requirements;
using Microsoft.AspNetCore.Authorization;

namespace EchoPhase.Security.Authorization.Builders
{
    [PolicyPrefix("scope:")]
    public class ScopePolicyBuilder : IPolicyBuilder
    {
        public AuthorizationPolicy? Build(string body)
        {
            var parts = body.Split(':', 2);
            bool requireAll = parts.Length != 2 || parts[0] != "any";
            var scopes = (parts.Length == 2 ? parts[1] : parts[0])
                .Split(',', StringSplitOptions.RemoveEmptyEntries);

            if (scopes.Length == 0) return null;

            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new ScopeRequirement(scopes, requireAll))
                .Build();
        }
    }
}
