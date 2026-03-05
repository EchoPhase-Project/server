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
