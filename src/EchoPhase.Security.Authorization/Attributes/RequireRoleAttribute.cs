// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.AspNetCore.Authorization;

namespace EchoPhase.Security.Authorization.Attributes
{
    public class RequireRoleAttribute : AuthorizeAttribute
    {
        public RequireRoleAttribute(params string[] roles)
            : base(BuildPolicyName(roles, requireAll: false)) { }

        public RequireRoleAttribute(bool requireAll, params string[] roles)
            : base(BuildPolicyName(roles, requireAll)) { }

        internal static string BuildPolicyName(string[] roles, bool requireAll) =>
            $"role:{(requireAll ? "all" : "any")}:{string.Join(",", roles.Order())}";
    }
}
