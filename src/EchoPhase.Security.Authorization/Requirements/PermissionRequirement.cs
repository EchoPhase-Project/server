// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.AspNetCore.Authorization;

namespace EchoPhase.Security.Authorization.Requirements
{
    public class PermissionRequirement : IAuthorizationRequirement
    {
        public string[] Resources { get; }
        public string[] Permissions { get; }

        public PermissionRequirement(string[] resources, string[] permissions)
        {
            Resources = resources;
            Permissions = permissions;
        }
    }
}
