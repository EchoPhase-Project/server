// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.AspNetCore.Authorization;

namespace EchoPhase.Security.Authorization.Requirements
{
    public class RoleRequirement : IAuthorizationRequirement
    {
        public string[] Roles { get; }
        public bool RequireAll { get; }

        public RoleRequirement(string[] roles, bool requireAll = false)
        {
            Roles = roles;
            RequireAll = requireAll;
        }
    }
}
