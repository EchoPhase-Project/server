using Microsoft.AspNetCore.Authorization;

namespace EchoPhase.Security.Authorization.Attributes
{
    public class RequireScopeAttribute : AuthorizeAttribute
    {
        public RequireScopeAttribute(params string[] scopes)
            : base(BuildPolicyName(scopes, requireAll: true)) { }

        public RequireScopeAttribute(bool requireAll, params string[] scopes)
            : base(BuildPolicyName(scopes, requireAll)) { }

        internal static string BuildPolicyName(string[] scopes, bool requireAll) =>
            $"scope:{(requireAll ? "all" : "any")}:{string.Join(",", scopes.Order())}";
    }
}
