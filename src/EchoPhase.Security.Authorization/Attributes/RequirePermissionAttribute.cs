// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.AspNetCore.Authorization;

namespace EchoPhase.Security.Authorization.Attributes
{
    public class RequirePermissionAttribute : AuthorizeAttribute
    {
        public RequirePermissionAttribute(string[] resources, string[] permissions)
            : base(BuildPolicyName(resources, permissions)) { }

        internal static string BuildPolicyName(string[] resources, string[] permissions) =>
            $"perm:{string.Join(",", resources.Order())}:{string.Join(",", permissions.Order())}";
    }
}
