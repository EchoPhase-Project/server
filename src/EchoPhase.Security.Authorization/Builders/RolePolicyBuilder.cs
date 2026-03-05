using Microsoft.AspNetCore.Authorization;
using EchoPhase.Security.Authorization.Requirements;
using EchoPhase.Security.Authorization.Attributes;

namespace EchoPhase.Security.Authorization.Builders
{
    [PolicyPrefix("role:")]
    public class RolePolicyBuilder : IPolicyBuilder
    {
        public AuthorizationPolicy? Build(string body)
        {
            var parts = body.Split(':', 2);
            bool requireAll = parts.Length == 2 && parts[0] == "all";
            var roles = (parts.Length == 2 ? parts[1] : parts[0])
                .Split(',', StringSplitOptions.RemoveEmptyEntries);

            if (roles.Length == 0) return null;

            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new RoleRequirement(roles, requireAll))
                .Build();
        }
    }
}
