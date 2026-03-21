// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Security.Authorization.Requirements;
using Microsoft.AspNetCore.Authorization;

namespace EchoPhase.Security.Authorization.Builders
{
    [PolicyPrefix("perm:")]
    public class PermissionPolicyBuilder : IPolicyBuilder
    {
        public AuthorizationPolicy? Build(string body)
        {
            var parts = body.Split(':', 2);
            if (parts.Length != 2) return null;

            var resources = parts[0].Split(',', StringSplitOptions.RemoveEmptyEntries);
            var permissions = parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries);

            if (resources.Length == 0 || permissions.Length == 0) return null;

            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(resources, permissions))
                .Build();
        }
    }
}
