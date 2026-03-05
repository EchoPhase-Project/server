using Microsoft.AspNetCore.Authorization;
using EchoPhase.Security.Authorization.Requirements;
using EchoPhase.Security.Authorization.Attributes;

namespace EchoPhase.Security.Authorization.Builders
{
    [PolicyPrefix("perm:")]
    public class PermissionPolicyBuilder : IPolicyBuilder
    {
        public AuthorizationPolicy? Build(string body)
        {
            var parts = body.Split(':', 2);
            if (parts.Length != 2) return null;

            var resources   = parts[0].Split(',', StringSplitOptions.RemoveEmptyEntries);
            var permissions = parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries);

            if (resources.Length == 0 || permissions.Length == 0) return null;

            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(resources, permissions))
                .Build();
        }
    }
}
